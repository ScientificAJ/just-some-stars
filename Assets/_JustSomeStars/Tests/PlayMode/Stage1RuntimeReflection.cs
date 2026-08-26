using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    internal static class Stage1RuntimeReflection
    {
        private const string RuntimeAssembly = "JustSomeStars.Runtime";

        internal static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", " + RuntimeAssembly);
            Assert.That(type, Is.Not.Null,
                $"Stage 1 runtime type is missing: {fullName}");
            return type;
        }

        internal static Component AddComponent(GameObject target, string fullName)
        {
            return target.AddComponent(RequireType(fullName));
        }

        internal static object Invoke(
            object target,
            string methodName,
            params object[] args)
        {
            var method = target.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.Instance)
                .Where(candidate => candidate.Name == methodName)
                .SingleOrDefault(candidate =>
                    candidate.GetParameters().Length == args.Length);
            Assert.That(method, Is.Not.Null,
                target.GetType().FullName + "." + methodName);
            return method.Invoke(target, args);
        }

        internal static object InvokeStatic(
            string fullName,
            string methodName,
            params object[] args)
        {
            var type = RequireType(fullName);
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(candidate => candidate.Name == methodName)
                .SingleOrDefault(candidate =>
                    candidate.GetParameters().Length == args.Length);
            Assert.That(method, Is.Not.Null, fullName + "." + methodName);
            return method.Invoke(null, args);
        }

        internal static T Read<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                target.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(target);
        }

        internal static ScriptableObject CreateConfig(string fullName)
        {
            return ScriptableObject.CreateInstance(RequireType(fullName));
        }

        internal static void Set(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                target.GetType().FullName + "." + propertyName);
            Assert.That(property.CanWrite, Is.True, propertyName);
            property.SetValue(target, value);
        }
    }
}
