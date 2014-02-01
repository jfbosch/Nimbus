﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Nimbus.Exceptions;
using Nimbus.Extensions;
using Nimbus.MessageContracts.Exceptions;

namespace Nimbus.Configuration
{
    public class PoorMansIoC
    {
        private readonly ConcurrentDictionary<Type, object> _components = new ConcurrentDictionary<Type, object>();
        private readonly ConcurrentDictionary<Type, Func<PoorMansIoC, object>> _factoryDelegates = new ConcurrentDictionary<Type, Func<PoorMansIoC, object>>();

        public void Register<T>(T instance)
        {
            RegisterInstance(instance);
        }

        public void Register<T>(Func<PoorMansIoC, T> factory)
        {
            RegisterFactoryDelegate(factory);
        }

        private void RegisterFactoryDelegate<T>(Func<PoorMansIoC, T> factory)
        {
            _factoryDelegates[typeof (T)] = pmioc => factory(pmioc);
        }

        private void RegisterInstance(object instance)
        {
            var concreteType = instance.GetType();
            _components[concreteType] = instance;

            var types = concreteType.GetInterfaces();
            foreach (var type in types)
            {
                _components[type] = instance;
            }
        }

        public T Resolve<T>()
        {
            return (T) Resolve(typeof (T));
        }

        private object Resolve(Type type)
        {
            try
            {
                return _components.GetOrAdd(type, ConstructObject);

            }
            catch (Exception exc)
            {
                throw new DependencyResolutionException("Could not resolve tyoe: {0}".FormatWith(type.FullName), exc);
            }
        }

        private object ConstructObject(Type type)
        {
            Func<PoorMansIoC, object> factory;
            if (_factoryDelegates.TryGetValue(type, out factory))
            {
                var instance = factory(this);
                RegisterInstance(instance);
                return instance;
            }
            else
            {
                var concreteType = ExtractConcreteTypeFor(type);
                var args = concreteType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                       .Single()
                                       .GetParameters()
                                       .Select(p => Resolve(p.ParameterType))
                                       .ToArray();

                var instance = Activator.CreateInstance(concreteType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
                RegisterInstance(instance);
                return instance;
            }
        }

        private static Type ExtractConcreteTypeFor(Type type)
        {
            var concreteType = typeof (PoorMansIoC).Assembly
                                                   .GetTypes()
                                                   .Where(type.IsAssignableFrom)
                                                   .Where(t => t.IsInstantiable())
                                                   .FirstOrDefault();

            if (concreteType == null) throw new DependencyResolutionException("Could not find a concrete type that implements {0}".FormatWith(type.FullName));

            return concreteType;
        }
    }

    [Serializable]
    public class DependencyResolutionException : BusException
    {
        public DependencyResolutionException()
        {
        }

        public DependencyResolutionException(string message) : base(message)
        {
        }

        public DependencyResolutionException(string message, Exception inner) : base(message, inner)
        {
        }

        protected DependencyResolutionException(SerializationInfo info,StreamingContext context) : base(info, context)
        {
        }
    }
}