﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ServiceBus.Messaging;
using Nimbus.Configuration.Settings;
using Nimbus.Extensions;
using Nimbus.Infrastructure;
using Nimbus.Infrastructure.RequestResponse;
using Nimbus.InfrastructureContracts;

namespace Nimbus.Configuration
{
    internal class CompetingEventMessagePumpsFactory
    {
        private readonly IQueueManager _queueManager;
        private readonly MessagingFactory _messagingFactory;
        private readonly ApplicationNameSetting _applicationName;
        private readonly CompetingEventHandlerTypesSetting _competingEventHandlerTypes;
        private readonly ICompetingEventBroker _competingEventBroker;
        private readonly ILogger _logger;

        public CompetingEventMessagePumpsFactory(IQueueManager queueManager,
                                                 MessagingFactory messagingFactory,
                                                 ApplicationNameSetting applicationName,
                                                 CompetingEventHandlerTypesSetting competingEventHandlerTypes,
                                                 ICompetingEventBroker competingEventBroker,
                                                 ILogger logger)
        {
            _queueManager = queueManager;
            _messagingFactory = messagingFactory;
            _applicationName = applicationName;
            _competingEventHandlerTypes = competingEventHandlerTypes;
            _competingEventBroker = competingEventBroker;
            _logger = logger;
        }

        public IEnumerable<IMessagePump> CreateAll()
        {
            _logger.Debug("Creating competing event message pumps");

            var eventTypes = _competingEventHandlerTypes.Value.SelectMany(ht => ht.GetGenericInterfacesClosing(typeof (IHandleCompetingEvent<>)))
                                                        .Select(gi => gi.GetGenericArguments().Single())
                                                        .OrderBy(t => t.FullName)
                                                        .Distinct()
                                                        .ToArray();

            foreach (var eventType in eventTypes)
            {
                _logger.Debug("Registering Message Pump for Competing Event type {0}", eventType.Name);

                var subscriptionName = String.Format("{0}", _applicationName);
                _queueManager.EnsureSubscriptionExists(eventType, subscriptionName);
                var topicPath = PathFactory.TopicPathFor(eventType);
                var subscriptionClient = _messagingFactory.CreateSubscriptionClient(topicPath, subscriptionName);
                var receiver = new NimbusMessageReceiver(subscriptionClient);

                var dispatcher = new CompetingEventMessageDispatcher(_competingEventBroker, eventType);

                var pump = new MessagePump(receiver, dispatcher, _logger);
                yield return pump;
            }
        }
    }
}