﻿namespace Barista.Jurassic.Library
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using Jurassic.Compiler;

    /// <summary>
    /// Provides functionality common to all JavaScript objects.
    /// </summary>
    [Serializable]
    public class ObjectInstance : System.Runtime.Serialization.IDeserializationCallback
    {
        // The script engine associated with this object.
        [NonSerialized]
        private ScriptEngine m_engine;

        // Internal prototype chain.
        private readonly ObjectInstance m_prototype;

        // Stores the property names and attributes for this object.
        private HiddenClassSchema m_schema;

        // Stores the property values for this object.
        private object[] m_propertyValues = new object[4];

        [Flags]
        private enum ObjectFlags
        {
            /// <summary>
            /// Indicates whether properties can be added to this object.
            /// </summary>
            Extensible = 1,
        }

        // Stores flags related to this object.
        private ObjectFlags m_flags = ObjectFlags.Extensible;



        //     INITIALIZATION
        //_________________________________________________________________________________________

        /// <summary>
        /// Creates an Object with the default prototype.
        /// </summary>
        /// <param name="engine"> The script engine associated with this object. </param>
        protected ObjectInstance(ScriptEngine engine)
            : this(engine, engine.Object.InstancePrototype)
        {
        }

        /// <summary>
        /// Called by derived classes to create a new object instance.
        /// </summary>
        /// <param name="prototype"> The next object in the prototype chain.  Cannot be <c>null</c>. </param>
        protected ObjectInstance(ObjectInstance prototype)
        {
            if (prototype == null)
                throw new ArgumentNullException("prototype");
            m_prototype = prototype;
            m_engine = prototype.Engine;
            m_schema = m_engine.EmptySchema;
        }

        /// <summary>
        /// Called by derived classes to create a new object instance.
        /// </summary>
        /// <param name="engine"> The script engine associated with this object. </param>
        /// <param name="prototype"> The next object in the prototype chain.  Can be <c>null</c>. </param>
        protected ObjectInstance(ScriptEngine engine, ObjectInstance prototype)
        {
            if (engine == null)
                throw new ArgumentNullException("engine");
            m_engine = engine;
            m_prototype = prototype;
            m_schema = engine.EmptySchema;
        }

        /// <summary>
        /// Creates an Object with no prototype to serve as the base prototype of all objects.
        /// </summary>
        /// <param name="engine"> The script engine associated with this object. </param>
        /// <returns> An Object with no prototype. </returns>
        internal static ObjectInstance CreateRootObject(ScriptEngine engine)
        {
            return new ObjectInstance(engine, null);
        }

        /// <summary>
        /// Creates an Object instance (use ObjectConstructor.Construct rather than this).
        /// </summary>
        /// <param name="prototype"> The next object in the prototype chain. </param>
        /// <returns> An Object instance. </returns>
        internal static ObjectInstance CreateRawObject(ObjectInstance prototype)
        {
            return new ObjectInstance(prototype);
        }



        //     SERIALIZATION
        //_________________________________________________________________________________________

        /// <summary>
        /// Runs when the entire object graph has been deserialized.
        /// </summary>
        /// <param name="sender"> Currently always <c>null</c>. </param>
        /// <remarks> Derived classes must call the base class implementation. </remarks>
        void System.Runtime.Serialization.IDeserializationCallback.OnDeserialization(object sender)
        {
            OnDeserializationCallback();
        }

        /// <summary>
        /// Runs when the entire object graph has been deserialized.
        /// </summary>
        /// <remarks> Derived classes must call the base class implementation. </remarks>
        protected virtual void OnDeserializationCallback()
        {
            // Set the engine to the per-thread deserialization script engine.
            m_engine = ScriptEngine.DeserializationEnvironment;
            if (m_engine == null)
                throw new InvalidOperationException("Set the ScriptEngine.DeserializationEnvironment property before deserializing any objects.");
        }

        //     .NET ACCESSOR PROPERTIES
        //_________________________________________________________________________________________

        /// <summary>
        /// Gets a reference to the script engine associated with this object.
        /// </summary>
        public ScriptEngine Engine
        {
            get { return m_engine; }
        }

        /// <summary>
        /// Gets the internal class name of the object.  Used by the default toString()
        /// implementation.
        /// </summary>
        protected virtual string InternalClassName
        {
#pragma warning disable 183
            // ReSharper disable once IsExpressionAlwaysTrue
            get { return this is ObjectInstance ? "Object" : GetType().Name; }
#pragma warning restore 183
        }

        /// <summary>
        /// Gets the next object in the prototype chain.  There is no corresponding property in
        /// javascript (it is is *not* the same as the prototype property), instead use
        /// Object.getPrototypeOf().
        /// </summary>
        public ObjectInstance Prototype
        {
            get { return m_prototype; }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the object can have new properties added
        /// to it.
        /// </summary>
        internal bool IsExtensible
        {
            get { return (m_flags & ObjectFlags.Extensible) != 0; }
            set
            {
                if (value)
                    throw new InvalidOperationException("Once an object has been made non-extensible it cannot be made extensible again.");
                m_flags &= ~ObjectFlags.Extensible;
            }
        }

        /// <summary>
        /// Gets or sets the value of a named property.
        /// </summary>
        /// <param name="propertyName"> The name of the property to get or set. </param>
        /// <returns> The property value, or <c>null</c> if the property doesn't exist. </returns>
        public object this[string propertyName]
        {
            get { return GetPropertyValue(propertyName); }
            set { SetPropertyValue(propertyName, value, false); }
        }

        /// <summary>
        /// Gets or sets the value of an array-indexed property.
        /// </summary>
        /// <param name="index"> The index of the property to retrieve. </param>
        /// <returns> The property value, or <c>null</c> if the property doesn't exist. </returns>
        public object this[uint index]
        {
            get { return GetPropertyValue(index); }
            set { SetPropertyValue(index, value, false); }
        }

        /// <summary>
        /// Gets or sets the value of an array-indexed property.
        /// </summary>
        /// <param name="index"> The index of the property to retrieve. </param>
        /// <returns> The property value, or <c>null</c> if the property doesn't exist. </returns>
        public object this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException("index");
                return GetPropertyValue((uint)index);
            }
            set
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException("index");
                SetPropertyValue((uint)index, value, false);
            }
        }

        /// <summary>
        /// Gets an enumerable list of every property name and value associated with this object.
        /// Does not include properties in the prototype chain.
        /// </summary>
        public virtual IEnumerable<PropertyNameAndValue> Properties
        {
            get
            {
                // Enumerate named properties.
                return m_schema.EnumeratePropertyNamesAndValues(InlinePropertyValues);
            }
        }



        //     INLINE CACHING
        //_________________________________________________________________________________________

        /// <summary>
        /// Gets an object to use as a cache key.  Adding, deleting, or modifying properties will
        /// cause the value of this property to change.
        /// </summary>
        public object InlineCacheKey
        {
            get { return m_schema; }
        }

        /// <summary>
        /// Gets the values stored against this object, one for each property.  The index to use
        /// can be retrieved from <see cref="InlineGetPropertyValue"/>,
        /// <see cref="InlineSetPropertyValue"/> or <see cref="InlineSetPropertyValueIfExists"/>.
        /// </summary>
        public object[] InlinePropertyValues
        {
            get { return m_propertyValues; }
        }

        /// <summary>
        /// Gets the value of the given property plus the information needed to speed up access to
        /// the property in future.
        /// </summary>
        /// <param name="name"> The name of the property. </param>
        /// <param name="cachedIndex"> Set to a zero-based index that can be used to get or set the
        /// property value in future (provided the cache key doesn't change). </param>
        /// <param name="cacheKey"> Set to a value that can be compared with the CacheKey property
        /// to determine if the cached index needs to be refreshed.  Can be set to <c>null</c> to
        /// prohibit caching. </param>
        /// <returns> The value of the property, or <c>null</c> if the property doesn't exist. </returns>
        public object InlineGetPropertyValue(string name, out int cachedIndex, out object cacheKey)
        {
            var propertyInfo = m_schema.GetPropertyIndexAndAttributes(name);
            if (propertyInfo.Exists)
            {
                // The property exists; it can be cached as long as it is not an accessor property.
                if ((propertyInfo.Attributes & (PropertyAttributes.IsAccessorProperty | PropertyAttributes.IsLengthProperty)) != 0)
                {
                    // Getters and the length property cannot be cached.
                    cachedIndex = -1;
                    cacheKey = null;

                    // Call the getter if there is one.
                    if (propertyInfo.IsAccessor)
                        return ((PropertyAccessorValue)m_propertyValues[propertyInfo.Index]).GetValue(this);

                    // Otherwise, the property is the "magic" length property.
                    return ((ArrayInstance)this).Length;
                }

                // The property can be cached.
                cachedIndex = propertyInfo.Index;
                cacheKey = InlineCacheKey;
                return m_propertyValues[cachedIndex];
            }

            // The property is in the prototype or is non-existent.
            cachedIndex = -1;
            cacheKey = null;
            return Prototype == null
              ? GetMissingPropertyValue(name)
              : Prototype.GetNamedPropertyValue(name, this);
        }

        /// <summary>
        /// Sets the value of the given property plus retrieves the information needed to speed up
        /// access to the property in future.  If a property with the given name does not exist, or
        /// exists in the prototype chain (and is not a setter) then a new property is created.
        /// </summary>
        /// <param name="name"> The name of the property to set. </param>
        /// <param name="value"> The desired value of the property. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be set (i.e. if the property is read-only or if the object is not extensible and a new
        /// property needs to be created). </param>
        /// <param name="cachedIndex"> Set to a zero-based index that can be used to get or set the
        /// property value in future (provided the cache key doesn't change). </param>
        /// <param name="cacheKey"> Set to a value that can be compared with the CacheKey property
        /// to determine if the cached index needs to be refreshed.  Can be set to <c>null</c> to
        /// prohibit caching. </param>
        public void InlineSetPropertyValue(string name, object value, bool throwOnError, out int cachedIndex, out object cacheKey)
        {
            var propertyInfo = m_schema.GetPropertyIndexAndAttributes(name);
            if (propertyInfo.Exists)
            {
                // The property exists; it can be cached as long as it is not read-only or an accessor property.
                if ((propertyInfo.Attributes & (PropertyAttributes.Writable | PropertyAttributes.IsAccessorProperty | PropertyAttributes.IsLengthProperty)) != PropertyAttributes.Writable)
                {
                    cachedIndex = -1;
                    cacheKey = null;
                    SetPropertyValue(name, value, throwOnError);
                }
                else
                {
                    // The property can be cached.
                    cachedIndex = propertyInfo.Index;
                    cacheKey = InlineCacheKey;
                    InlinePropertyValues[cachedIndex] = value ?? Undefined.Value;
                }
            }
            else
            {
                // The property is in the prototype or is non-existent.
                cachedIndex = -1;
                cacheKey = null;
                SetPropertyValue(name, value, throwOnError);
            }
        }

        /// <summary>
        /// Sets the value of the given property plus retrieves the information needed to speed up
        /// access to the property in future.  If a property with the given name exists, but only
        /// in the prototype chain, then a new property is created (unless the property is a
        /// setter, in which case the setter is called and no property is created).  If the
        /// property does not exist at all, then no property is created and the method returns
        /// <c>false</c>.
        /// </summary>
        /// <param name="name"> The name of the property to set. </param>
        /// <param name="value"> The desired value of the property.  This must be a javascript
        /// primitive (double, string, etc) or a class derived from <see cref="ObjectInstance"/>. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be set (i.e. if the property is read-only or if the object is not extensible and a new
        /// property needs to be created). </param>
        /// <param name="cachedIndex"> Set to a zero-based index that can be used to get or set the
        /// property value in future (provided the cache key doesn't change). </param>
        /// <param name="cacheKey"> Set to a value that can be compared with the CacheKey property
        /// to determine if the cached index needs to be refreshed.  Can be set to <c>null</c> to
        /// prohibit caching. </param>
        /// <returns> <c>true</c> if the property value exists; <c>false</c> otherwise. </returns>
        public bool InlineSetPropertyValueIfExists(string name, object value, bool throwOnError, out int cachedIndex, out object cacheKey)
        {
            var propertyInfo = m_schema.GetPropertyIndexAndAttributes(name);
            if (propertyInfo.Exists)
            {
                // The property exists; it can be cached as long as it is not read-only or an accessor property.
                if ((propertyInfo.Attributes & (PropertyAttributes.Writable | PropertyAttributes.IsAccessorProperty | PropertyAttributes.IsLengthProperty)) != PropertyAttributes.Writable)
                {
                    cachedIndex = -1;
                    cacheKey = null;
                    return SetPropertyValueIfExists(name, value, throwOnError);
                }

                // The property can be cached.
                cachedIndex = propertyInfo.Index;
                cacheKey = InlineCacheKey;
                InlinePropertyValues[cachedIndex] = value ?? Undefined.Value;
                return true;
            }

            // The property is in the prototype or is non-existent.
            cachedIndex = -1;
            cacheKey = null;
            return SetPropertyValueIfExists(name, value, throwOnError);
        }



        //     PROPERTY MANAGEMENT
        //_________________________________________________________________________________________

        ///// <summary>
        ///// Gets the property descriptor for the property with the given name.  The prototype
        ///// chain is not searched.
        ///// </summary>
        ///// <param name="propertyName"> The name of the property. </param>
        ///// <returns> A property descriptor containing the property value and attributes.  The
        ///// result will be <c>PropertyDescriptor.Undefined</c> if the property doesn't exist. </returns>
        //internal virtual PropertyDescriptor GetOwnProperty(string propertyName)
        //{
        //    PropertyAttributes attributes;
        //    int index = this.schema.GetPropertyIndexAndAttributes(propertyName, out attributes);
        //    if (index == -1)
        //        return PropertyDescriptor.Undefined;
        //    return new PropertyDescriptor(this.propertyValues[index], attributes);
        //}

        ///// <summary>
        ///// Gets the property descriptor for the property with the given array index.  The
        ///// prototype chain is not searched.
        ///// </summary>
        ///// <param name="index"> The array index of the property. </param>
        ///// <returns> A property descriptor containing the property value and attributes.  The
        ///// result will be <c>PropertyDescriptor.Undefined</c> if the property doesn't exist. </returns>
        //internal virtual PropertyDescriptor GetOwnProperty(uint index)
        //{
        //    return GetOwnProperty(index.ToString());
        //}

        ///// <summary>
        ///// Gets the property descriptor for the property with the given name.
        ///// </summary>
        ///// <param name="propertyName"> The name of the property. </param>
        ///// <returns> A property descriptor containing the property value and attributes.  The
        ///// value will be <c>PropertyDescriptor.Undefined</c> if the property doesn't exist. </returns>
        //internal PropertyDescriptor GetProperty(string propertyName)
        //{

        //}

        /// <summary>
        /// Determines if a property with the given name exists.
        /// </summary>
        /// <param name="propertyName"> The name of the property to check. </param>
        /// <returns> <c>true</c> if the property exists on this object or in the prototype chain;
        /// <c>false</c> otherwise. </returns>
        public bool HasProperty(string propertyName)
        {
            return GetPropertyValue(propertyName) != null;
        }

        /// <summary>
        /// Gets the value of the property with the given array index.
        /// </summary>
        /// <param name="index"> The array index of the property. </param>
        /// <returns> The value of the property, or <c>null</c> if the property doesn't exist. </returns>
        /// <remarks> The prototype chain is searched if the property does not exist directly on
        /// this object. </remarks>
        public object GetPropertyValue(uint index)
        {
            return GetPropertyValue(index, this);
        }

        /// <summary>
        /// Gets the value of the property with the given array index.
        /// </summary>
        /// <param name="index"> The array index of the property. </param>
        /// <param name="thisValue"> The value of the "this" keyword inside a getter. </param>
        /// <returns> The value of the property, or <c>null</c> if the property doesn't exist. </returns>
        /// <remarks> The prototype chain is searched if the property does not exist directly on
        /// this object. </remarks>
        private object GetPropertyValue(uint index, ObjectInstance thisValue)
        {
            // Get the descriptor for the property.
            var property = GetOwnPropertyDescriptor(index);
            if (property.Exists)
            {
                // The property was found!  Call the getter if there is one.
                object value = property.Value;
                var accessor = value as PropertyAccessorValue;
                if (accessor != null)
                    return accessor.GetValue(thisValue);
                return value;
            }

            // The property might exist in the prototype.
            if (m_prototype == null)
                return thisValue.GetMissingPropertyValue(index.ToString(CultureInfo.InvariantCulture));
            return m_prototype.GetPropertyValue(index, thisValue);
        }

        /// <summary>
        /// Gets the value of the property with the given name.
        /// </summary>
        /// <param name="propertyName"> The name of the property. </param>
        /// <returns> The value of the property, or <c>null</c> if the property doesn't exist. </returns>
        /// <remarks> The prototype chain is searched if the property does not exist directly on
        /// this object. </remarks>
        public object GetPropertyValue(string propertyName)
        {
            // Check if the property is an indexed property.
            uint arrayIndex = ArrayInstance.ParseArrayIndex(propertyName);
            if (arrayIndex != uint.MaxValue)
                return GetPropertyValue(arrayIndex);

            // Otherwise, the property is a name.
            return GetNamedPropertyValue(propertyName, this);
        }

        /// <summary>
        /// Gets the value of the property with the given name.  The name cannot be an array index.
        /// </summary>
        /// <param name="propertyName"> The name of the property.  The name cannot be an array index. </param>
        /// <param name="thisValue"> The value of the "this" keyword inside a getter. </param>
        /// <returns> The value of the property, or <c>null</c> if the property doesn't exist. </returns>
        /// <remarks> The prototype chain is searched if the property does not exist directly on
        /// this object. </remarks>
        private object GetNamedPropertyValue(string propertyName, ObjectInstance thisValue)
        {
            ObjectInstance prototypeObject = this;
            do
            {
                // Retrieve information about the property.
                var property = prototypeObject.m_schema.GetPropertyIndexAndAttributes(propertyName);
                if (property.Exists)
                {
                    // The property was found!
                    object value = prototypeObject.m_propertyValues[property.Index];
                    if ((property.Attributes & (PropertyAttributes.IsAccessorProperty | PropertyAttributes.IsLengthProperty)) == 0)
                        return value;

                    // Call the getter if there is one.
                    if (property.IsAccessor)
                        return ((PropertyAccessorValue)value).GetValue(thisValue);

                    // Otherwise, the property is the "magic" length property.
                    return ((ArrayInstance)prototypeObject).Length;
                }

                // Traverse the prototype chain.
                prototypeObject = prototypeObject.m_prototype;
            } while (prototypeObject != null);

            // The property doesn't exist.
            return thisValue.GetMissingPropertyValue(propertyName);
        }

        /// <summary>
        /// Retrieves the value of a property which doesn't exist on the object.  This method can
        /// be overridden to effectively construct properties on the fly.  The default behavior is
        /// to return <c>undefined</c>.
        /// </summary>
        /// <param name="propertyName"> The name of the missing property. </param>
        /// <returns> The value of the missing property. </returns>
        /// <remarks> When overriding, call the base class implementation only if you want to
        /// revert to the default behavior. </remarks>
        protected virtual object GetMissingPropertyValue(string propertyName)
        {
            if (m_prototype == null)
                return null;
            return m_prototype.GetMissingPropertyValue(propertyName);
        }

        /// <summary>
        /// Gets a descriptor for the property with the given array index.
        /// </summary>
        /// <param name="index"> The array index of the property. </param>
        /// <returns> A property descriptor containing the property value and attributes. </returns>
        /// <remarks> The prototype chain is not searched. </remarks>
        public virtual PropertyDescriptor GetOwnPropertyDescriptor(uint index)
        {
            var property = m_schema.GetPropertyIndexAndAttributes(index.ToString(CultureInfo.InvariantCulture));
            if (property.Exists)
                return new PropertyDescriptor(m_propertyValues[property.Index], property.Attributes);
            return PropertyDescriptor.Undefined;
        }

        /// <summary>
        /// Gets a descriptor for the property with the given name.
        /// </summary>
        /// <param name="propertyName"> The name of the property. </param>
        /// <returns> A property descriptor containing the property value and attributes. </returns>
        /// <remarks> The prototype chain is not searched. </remarks>
        public PropertyDescriptor GetOwnPropertyDescriptor(string propertyName)
        {
            // Check if the property is an indexed property.
            uint arrayIndex = ArrayInstance.ParseArrayIndex(propertyName);
            if (arrayIndex != uint.MaxValue)
                return GetOwnPropertyDescriptor(arrayIndex);

            // Retrieve information about the property.
            var property = m_schema.GetPropertyIndexAndAttributes(propertyName);
            if (property.Exists)
            {
                if (property.IsLength == false)
                    return new PropertyDescriptor(m_propertyValues[property.Index], property.Attributes);

                // The property is the "magic" length property.
                return new PropertyDescriptor(((ArrayInstance)this).Length, property.Attributes);
            }

            // The property doesn't exist.
            return PropertyDescriptor.Undefined;
        }

        /// <summary>
        /// Sets the value of the property with the given array index.  If a property with the
        /// given index does not exist, or exists in the prototype chain (and is not a setter) then
        /// a new property is created.
        /// </summary>
        /// <param name="index"> The array index of the property to set. </param>
        /// <param name="value"> The value to set the property to.  This must be a javascript
        /// primitive (double, string, etc) or a class derived from <see cref="ObjectInstance"/>. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be set.  This can happen if the property is read-only or if the object is sealed. </param>
        public virtual void SetPropertyValue(uint index, object value, bool throwOnError)
        {
            string indexStr = index.ToString(CultureInfo.InvariantCulture);
            bool exists = SetPropertyValueIfExists(indexStr, value, throwOnError);
            if (exists == false)
            {
                // The property doesn't exist - add it.
                AddProperty(indexStr, value, PropertyAttributes.FullAccess, throwOnError);
            }
        }

        /// <summary>
        /// Sets the value of the property with the given name.  If a property with the given name
        /// does not exist, or exists in the prototype chain (and is not a setter) then a new
        /// property is created.
        /// </summary>
        /// <param name="propertyName"> The name of the property to set. </param>
        /// <param name="value"> The value to set the property to.  This must be a javascript
        /// primitive (double, string, etc) or a class derived from <see cref="ObjectInstance"/>. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be set (i.e. if the property is read-only or if the object is not extensible and a new
        /// property needs to be created). </param>
        public void SetPropertyValue(string propertyName, object value, bool throwOnError)
        {
            // Check if the property is an indexed property.
            uint arrayIndex = ArrayInstance.ParseArrayIndex(propertyName);
            if (arrayIndex != uint.MaxValue)
            {
                SetPropertyValue(arrayIndex, value, throwOnError);
                return;
            }

            bool exists = SetPropertyValueIfExists(propertyName, value, throwOnError);
            if (exists == false)
            {
                // The property doesn't exist - add it.
                AddProperty(propertyName, value, PropertyAttributes.FullAccess, throwOnError);
            }
        }

        /// <summary>
        /// Sets the value of the given property.  If a property with the given name exists, but
        /// only in the prototype chain, then a new property is created (unless the property is a
        /// setter, in which case the setter is called and no property is created).  If the
        /// property does not exist at all, then no property is created and the method returns
        /// <c>false</c>.  This method is used to set the value of a variable reference within a
        /// with statement.
        /// </summary>
        /// <param name="propertyName"> The name of the property to set.  Cannot be an array index. </param>
        /// <param name="value"> The desired value of the property.  This must be a javascript
        /// primitive (double, string, etc) or a class derived from <see cref="ObjectInstance"/>. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be set (i.e. if the property is read-only or if the object is not extensible and a new
        /// property needs to be created). </param>
        /// <returns> <c>true</c> if the property value exists; <c>false</c> otherwise. </returns>
        public bool SetPropertyValueIfExists(string propertyName, object value, bool throwOnError)
        {
            // Do not store nulls - null represents a non-existant value.
            value = value ?? Undefined.Value;

            // Retrieve information about the property.
            var property = m_schema.GetPropertyIndexAndAttributes(propertyName);
            if (property.Exists)
            {
                // Check if the property is read-only.
                if (property.IsWritable == false)
                {
                    // The property is read-only.
                    if (throwOnError)
                        throw new JavaScriptException(Engine, "TypeError", string.Format("The property '{0}' is read-only.", propertyName));
                    return true;
                }

                if ((property.Attributes & (PropertyAttributes.IsAccessorProperty | PropertyAttributes.IsLengthProperty)) == 0)
                {
                    // The property contains a simple value.  Set the property value.
                    m_propertyValues[property.Index] = value;
                }
                else if (property.IsAccessor)
                {
                    // The property contains an accessor function.  Set the property value by calling the accessor.
                    ((PropertyAccessorValue)m_propertyValues[property.Index]).SetValue(this, value);
                }
                else
                {
                    // Otherwise, the property is the "magic" length property.
                    var length = TypeConverter.ToNumber(value);
                    var lengthUint32 = TypeConverter.ToUint32(length);

                    if (Double.IsNaN(length) || Math.Abs(length - lengthUint32) > Double.Epsilon)
                        throw new JavaScriptException(Engine, "RangeError", "Invalid array length");

                    ((ArrayInstance)this).Length = lengthUint32;
                }
                return true;
            }

            // Search the prototype chain for a accessor function.  If one is found, it will
            // prevent the creation of a new property.
            bool propertyExistsInPrototype = false;
            ObjectInstance prototypeObject = m_prototype;
            while (prototypeObject != null)
            {
                property = prototypeObject.m_schema.GetPropertyIndexAndAttributes(propertyName);
                if (property.Exists)
                {
                    if (property.IsAccessor)
                    {
                        // The property contains an accessor function.  Set the property value by calling the accessor.
                        ((PropertyAccessorValue)prototypeObject.m_propertyValues[property.Index]).SetValue(this, value);
                        return true;
                    }
                    propertyExistsInPrototype = true;
                    break;
                }
                prototypeObject = prototypeObject.m_prototype;
            }

            // If the property exists in the prototype, create a new property.
            if (propertyExistsInPrototype)
            {
                AddProperty(propertyName, value, PropertyAttributes.FullAccess, throwOnError);
                return true;
            }

            // The property does not exist.
            return false;
        }

        /// <summary>
        /// Deletes the property with the given array index.
        /// </summary>
        /// <param name="index"> The array index of the property to delete. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be set because the property was marked as non-configurable.  </param>
        /// <returns> <c>true</c> if the property was successfully deleted, or if the property did
        /// not exist; <c>false</c> if the property was marked as non-configurable and
        /// <paramref name="throwOnError"/> was <c>false</c>. </returns>
        public virtual bool Delete(uint index, bool throwOnError)
        {
            string indexStr = index.ToString(CultureInfo.InvariantCulture);

            // Retrieve the attributes for the property.
            var propertyInfo = m_schema.GetPropertyIndexAndAttributes(indexStr);
            if (propertyInfo.Exists == false)
                return true;    // Property doesn't exist - delete succeeded!

            // Delete the property.
            m_schema = m_schema.DeleteProperty(indexStr);
            return true;
        }

        /// <summary>
        /// Deletes the property with the given name.
        /// </summary>
        /// <param name="propertyName"> The name of the property to delete. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be set because the property was marked as non-configurable.  </param>
        /// <returns> <c>true</c> if the property was successfully deleted, or if the property did
        /// not exist; <c>false</c> if the property was marked as non-configurable and
        /// <paramref name="throwOnError"/> was <c>false</c>. </returns>
        public bool Delete(string propertyName, bool throwOnError)
        {
            // Check if the property is an indexed property.
            uint arrayIndex = ArrayInstance.ParseArrayIndex(propertyName);
            if (arrayIndex != uint.MaxValue)
                return Delete(arrayIndex, throwOnError);

            // Retrieve the attributes for the property.
            var propertyInfo = m_schema.GetPropertyIndexAndAttributes(propertyName);
            if (propertyInfo.Exists == false)
                return true;    // Property doesn't exist - delete succeeded!

            // Check if the property can be deleted.
            if (propertyInfo.IsConfigurable == false)
            {
                if (throwOnError)
                    throw new JavaScriptException(Engine, "TypeError", string.Format("The property '{0}' cannot be deleted.", propertyName));
                return false;
            }

            // Delete the property.
            m_schema = m_schema.DeleteProperty(propertyName);
            return true;
        }

        /// <summary>
        /// Defines or redefines the value and attributes of a property.  The prototype chain is
        /// not searched so if the property exists but only in the prototype chain a new property
        /// will be created.
        /// </summary>
        /// <param name="propertyName"> The name of the property to modify. </param>
        /// <param name="descriptor"> The property value and attributes. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be set.  This can happen if the property is not configurable or the object is sealed. </param>
        /// <returns> <c>true</c> if the property was successfully modified; <c>false</c> otherwise. </returns>
        public virtual bool DefineProperty(string propertyName, PropertyDescriptor descriptor, bool throwOnError)
        {
            // Retrieve info on the property.
            var current = m_schema.GetPropertyIndexAndAttributes(propertyName);

            if (current.Exists == false)
            {
                // Create a new property.
                return AddProperty(propertyName, descriptor.Value, descriptor.Attributes, throwOnError);
            }

            // If the current property is not configurable, then the only change that is allowed is
            // a change from one simple value to another (i.e. accessors are not allowed) and only
            // if the writable attribute is set.
            if (current.IsConfigurable == false)
            {
                // Get the current value of the property.
                object currentValue = m_propertyValues[current.Index];
                object getter = null, setter = null;
                var propertyAccessorValue = currentValue as PropertyAccessorValue;
                if (propertyAccessorValue != null)
                {
                    getter = propertyAccessorValue.Getter;
                    setter = propertyAccessorValue.Setter;
                }

                // Check if the modification is allowed.
                if (descriptor.Attributes != current.Attributes ||
                    (descriptor.IsAccessor && (getter != descriptor.Getter || setter != descriptor.Setter)) ||
                    (descriptor.IsAccessor == false && current.IsWritable == false && TypeComparer.SameValue(currentValue, descriptor.Value) == false))
                {
                    if (throwOnError)
                        throw new JavaScriptException(Engine, "TypeError", string.Format("The property '{0}' is non-configurable.", propertyName));
                    return false;
                }
            }

            // Set the property value and attributes.
            m_schema = m_schema.SetPropertyAttributes(propertyName, descriptor.Attributes);
            m_propertyValues[current.Index] = descriptor.Value;

            return true;
        }

        /// <summary>
        /// Adds a property to this object.  The property must not already exist.
        /// </summary>
        /// <param name="propertyName"> The name of the property to add. </param>
        /// <param name="value"> The desired value of the property.  This can be a
        /// <see cref="PropertyAccessorValue"/>. </param>
        /// <param name="attributes"> Attributes describing how the property may be modified. </param>
        /// <param name="throwOnError"> <c>true</c> to throw an exception if the property could not
        /// be added (i.e. if the object is not extensible). </param>
        /// <returns> <c>true</c> if the property was successfully added; <c>false</c> otherwise. </returns>
        private bool AddProperty(string propertyName, object value, PropertyAttributes attributes, bool throwOnError)
        {
            // Make sure adding a property is allowed.
            if (IsExtensible == false)
            {
                if (throwOnError)
                    throw new JavaScriptException(Engine, "TypeError", string.Format("The property '{0}' cannot be created as the object is not extensible.", propertyName));
                return false;
            }

            // To avoid running out of memory, restrict the number of properties.
            if (m_schema.PropertyCount == 16384)
                throw new JavaScriptException(m_engine, "Error", "Maximum number of named properties reached.");

            // Do not store nulls - null represents a non-existant value.
            value = value ?? Undefined.Value;

            // Add a new property to the schema.
            m_schema = m_schema.AddProperty(propertyName, attributes);

            // Check if the value array needs to be resized.
            int propertyIndex = m_schema.NextValueIndex - 1;
            if (propertyIndex >= InlinePropertyValues.Length)
                Array.Resize(ref m_propertyValues, InlinePropertyValues.Length * 2);

            // Set the value of the property.
            m_propertyValues[propertyIndex] = value;

            // Success.
            return true;
        }

        /// <summary>
        /// Sets a property value and attributes, or adds a new property if it doesn't already
        /// exist.  Any existing attributes are ignored (and not modified).
        /// </summary>
        /// <param name="propertyName"> The name of the property. </param>
        /// <param name="value"> The intended value of the property. </param>
        /// <param name="attributes"> Attributes that indicate whether the property is writable,
        /// configurable and enumerable. Default: PropertyAttributes.Sealed</param>
        /// <param name="overwriteAttributes"> Indicates whether to overwrite any existing attributes. Default: False </param>
        internal void FastSetProperty(string propertyName, object value, PropertyAttributes attributes, bool overwriteAttributes)
        {
            int index = m_schema.GetPropertyIndex(propertyName);
            if (index < 0)
            {
                // The property is doesn't exist - add a new property.
                AddProperty(propertyName, value, attributes, false);
                return;
            }
            if (overwriteAttributes)
                m_schema = m_schema.SetPropertyAttributes(propertyName, attributes);
            m_propertyValues[index] = value;
        }



        //     OTHERS
        //_________________________________________________________________________________________

        /// <summary>
        /// Returns a primitive value that represents the current object.  Used by the addition and
        /// equality operators.
        /// </summary>
        /// <param name="typeHint"> Indicates the preferred type of the result. </param>
        /// <returns> A primitive value that represents the current object. </returns>
        protected internal virtual object GetPrimitiveValue(PrimitiveTypeHint typeHint)
        {
            if (typeHint == PrimitiveTypeHint.None || typeHint == PrimitiveTypeHint.Number)
            {

                // Try calling valueOf().
                object valueOfResult;
                if (TryCallMemberFunction(out valueOfResult, "valueOf"))
                {
                    // Return value must be primitive.
                    if (valueOfResult is double || TypeUtilities.IsPrimitive(valueOfResult))
                        return valueOfResult;
                }

                // Try calling toString().
                object toStringResult;
                if (TryCallMemberFunction(out toStringResult, "toString"))
                {
                    // Return value must be primitive.
                    if (toStringResult is string || TypeUtilities.IsPrimitive(toStringResult))
                        return toStringResult;
                }

            }
            else
            {

                // Try calling toString().
                object toStringResult;
                if (TryCallMemberFunction(out toStringResult, "toString"))
                {
                    // Return value must be primitive.
                    if (toStringResult is string || TypeUtilities.IsPrimitive(toStringResult))
                        return toStringResult;
                }

                // Try calling valueOf().
                object valueOfResult;
                if (TryCallMemberFunction(out valueOfResult, "valueOf"))
                {
                    // Return value must be primitive.
                    if (valueOfResult is double || TypeUtilities.IsPrimitive(valueOfResult))
                        return valueOfResult;
                }

            }

            throw new JavaScriptException(Engine, "TypeError", "Attempted conversion of the object to a primitive value failed.  Check the toString() and valueOf() functions.");
        }

        /// <summary>
        /// Calls the function with the given name.  The function must exist on this object or an
        /// exception will be thrown.
        /// </summary>
        /// <param name="functionName"> The name of the function to call. </param>
        /// <param name="parameters"> The parameters to pass to the function. </param>
        /// <returns> The result of calling the function. </returns>
        public object CallMemberFunction(string functionName, params object[] parameters)
        {
            var function = GetPropertyValue(functionName);
            if (function == null)
                throw new JavaScriptException(Engine, "TypeError", string.Format("Object {0} has no method '{1}'", this, functionName));
            if ((function is FunctionInstance) == false)
                throw new JavaScriptException(Engine, "TypeError", string.Format("Property '{1}' of object {0} is not a function", this, functionName));
            return ((FunctionInstance)function).CallLateBound(this, parameters);
        }

        /// <summary>
        /// Calls the function with the given name.
        /// </summary>
        /// <param name="result"> The result of calling the function. </param>
        /// <param name="functionName"> The name of the function to call. </param>
        /// <param name="parameters"> The parameters to pass to the function. </param>
        /// <returns> <c>true</c> if the function was called successfully; <c>false</c> otherwise. </returns>
        public bool TryCallMemberFunction(out object result, string functionName, params object[] parameters)
        {
            var function = GetPropertyValue(functionName);
            if ((function is FunctionInstance) == false)
            {
                result = null;
                return false;
            }
            result = ((FunctionInstance)function).CallLateBound(this, parameters);
            return true;
        }

        /// <summary>
        /// Returns a string representing this object.
        /// </summary>
        /// <returns> A string representing this object. </returns>
        public override string ToString()
        {
            try
            {
                // Does a toString method/property exist?
                if (HasProperty("toString"))
                {
                    // Call the toString() method.
                    var result = CallMemberFunction("toString");

                    // Make sure we don't recursively loop forever.
                    if (result == this)
                        return "<error>";

                    // Convert to a string.
                    return TypeConverter.ToString(result);
                }

                // Otherwise, return the type name.
                var constructor = this["constructor"] as FunctionInstance;
                return constructor != null
                         ? string.Format("[{0}]", constructor.Name)
                         : "<unknown>";
            }
            catch (JavaScriptException)
            {
                return "<error>";
            }
        }



        //     JAVASCRIPT FUNCTIONS
        //_________________________________________________________________________________________

        /// <summary>
        /// Determines if a property with the given name exists on this object.
        /// </summary>
        /// <param name="engine"> The associated script engine. </param>
        /// <param name="thisObject">The This Object.</param>
        /// <param name="propertyName"> The name of the property. </param>
        /// <returns> <c>true</c> if a property with the given name exists on this object,
        /// <c>false</c> otherwise. </returns>
        /// <remarks> Objects in the prototype chain are not considered. </remarks>
        [JSInternalFunction(Name = "hasOwnProperty", Flags = JSFunctionFlags.HasEngineParameter | JSFunctionFlags.HasThisObject)]
        public static bool HasOwnProperty(ScriptEngine engine, object thisObject, string propertyName)
        {
            TypeUtilities.VerifyThisObject(engine, thisObject, "hasOwnProperty");
            return TypeConverter.ToObject(engine, thisObject).GetOwnPropertyDescriptor(propertyName).Exists;
        }

        /// <summary>
        /// Determines if this object is in the prototype chain of the given object.
        /// </summary>
        /// <param name="engine"> The associated script engine. </param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="obj"> The object to check. </param>
        /// <returns> <c>true</c> if this object is in the prototype chain of the given object;
        /// <c>false</c> otherwise. </returns>
        [JSInternalFunction(Name = "isPrototypeOf", Flags = JSFunctionFlags.HasEngineParameter | JSFunctionFlags.HasThisObject)]
        public static bool IsPrototypeOf(ScriptEngine engine, object thisObject, object obj)
        {
            if ((obj is ObjectInstance) == false)
                return false;
            TypeUtilities.VerifyThisObject(engine, thisObject, "isPrototypeOf");
            var obj2 = obj as ObjectInstance;
            while (true)
            {
                obj2 = obj2.Prototype;
                if (obj2 == null)
                    return false;
                if (obj2 == thisObject)
                    return true;
            }
        }

        /// <summary>
        /// Determines if a property with the given name exists on this object and is enumerable.
        /// </summary>
        /// <param name="engine"> The associated script engine. </param>
        /// <param name="thisObject">The this Object.</param>
        /// <param name="propertyName"> The name of the property. </param>
        /// <returns> <c>true</c> if a property with the given name exists on this object and is
        /// enumerable, <c>false</c> otherwise. </returns>
        /// <remarks> Objects in the prototype chain are not considered. </remarks>
        [JSInternalFunction(Name = "propertyIsEnumerable", Flags = JSFunctionFlags.HasEngineParameter | JSFunctionFlags.HasThisObject)]
        public static bool PropertyIsEnumerable(ScriptEngine engine, object thisObject, string propertyName)
        {
            TypeUtilities.VerifyThisObject(engine, thisObject, "propertyIsEnumerable");
            var property = TypeConverter.ToObject(engine, thisObject).GetOwnPropertyDescriptor(propertyName);
            return property.Exists && property.IsEnumerable;
        }

        /// <summary>
        /// Returns a locale-dependant string representing the current object.
        /// </summary>
        /// <returns> Returns a locale-dependant string representing the current object. </returns>
        [JSInternalFunction(Name = "toLocaleString")]
        public string ToLocaleString()
        {
            return TypeConverter.ToString(CallMemberFunction("toString"));
        }

        /// <summary>
        /// Returns a primitive value associated with the object.
        /// </summary>
        /// <returns> A primitive value associated with the object. </returns>
        [JSInternalFunction(Name = "valueOf")]
        public ObjectInstance ValueOf()
        {
            return this;
        }

        /// <summary>
        /// Returns a string representing the current object.
        /// </summary>
        /// <param name="engine">The Script Engine.</param>
        /// <param name="thisObject"> The value of the "this" keyword. </param>
        /// <returns> A string representing the current object. </returns>
        [JSInternalFunction(Name = "toString", Flags = JSFunctionFlags.HasEngineParameter | JSFunctionFlags.HasThisObject)]
        public static string ToStringJS(ScriptEngine engine, object thisObject)
        {
            if (thisObject == null || thisObject == Undefined.Value)
                return "[object Undefined]";
            if (thisObject == Null.Value)
                return "[object Null]";
            return string.Format("[object {0}]", TypeConverter.ToObject(engine, thisObject).InternalClassName);
        }



        //     ATTRIBUTE-BASED PROTOTYPE POPULATION
        //_________________________________________________________________________________________

        private class MethodGroup
        {
            public List<JSBinderMethod> Methods;
            public int Length;
            public PropertyAttributes PropertyAttributes;
        }

        /// <summary>
        /// Populates the object with functions by searching a .NET type for methods marked with
        /// the [JSFunction] attribute.  Should be called only once at startup.  Also automatically
        /// populates properties marked with the [JSProperty] attribute.
        /// </summary>
        internal protected void PopulateFunctions()
        {
            PopulateFunctions(null);
        }

        /// <summary>
        /// Populates the object with functions by searching a .NET type for methods marked with
        /// the [JSFunction] attribute.  Should be called only once at startup.  Also automatically
        /// populates properties marked with the [JSProperty] attribute.
        /// </summary>
        /// <param name="type"> The type to search for methods. </param>
        internal protected void PopulateFunctions(Type type)
        {
            PopulateFunctions(type, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        }

        /// <summary>
        /// Populates the object with functions by searching a .NET type for methods marked with
        /// the [JSFunction] attribute.  Should be called only once at startup.  Also automatically
        /// populates properties marked with the [JSProperty] attribute.
        /// </summary>
        /// <param name="type"> The type to search for methods. </param>
        /// <param name="bindingFlags"> The binding flags to use to search for properties and methods. </param>
        internal protected void PopulateFunctions(Type type, BindingFlags bindingFlags)
        {
            if (type == null)
                type = GetType();

            // Group the methods on the given type by name.
            var functions = new Dictionary<string, MethodGroup>(20);
            var methods = type.GetMethods(bindingFlags);
            foreach (var method in methods)
            {
                // Make sure the method has the [JSInternalFunction] attribute.
                var attribute = (JSFunctionAttribute)Attribute.GetCustomAttribute(method, typeof(JSFunctionAttribute));
                if (attribute == null)
                    continue;

                // Determine the name of the method.
                string name = attribute.Name ?? method.Name;

                // Get a reference to the method group.
                MethodGroup methodGroup;
                if (functions.ContainsKey(name) == false)
                {
                    methodGroup = new MethodGroup { Methods = new List<JSBinderMethod>(1), Length = -1 };
                    functions.Add(name, methodGroup);
                }
                else
                    methodGroup = functions[name];

                // Internal functions return nulls as undefined.
                if (attribute is JSInternalFunctionAttribute)
                    attribute.Flags |= JSFunctionFlags.ConvertNullReturnValueToUndefined;

                // Add the method to the list.
                methodGroup.Methods.Add(new JSBinderMethod(method, attribute.Flags));

                // If the length doesn't equal -1, that indicates an explicit length has been set.
                // Make sure it is consistant with the other methods.
                if (attribute.Length >= 0)
                {
                    if (methodGroup.Length != -1 && methodGroup.Length != attribute.Length)
                        throw new InvalidOperationException(string.Format("Inconsistant Length property detected on {0}.", method));
                    methodGroup.Length = attribute.Length;
                }

                // Check property attributes.
                var descriptorAttributes = PropertyAttributes.Sealed;
                if (attribute.IsEnumerable)
                    descriptorAttributes |= PropertyAttributes.Enumerable;
                if (attribute.IsConfigurable)
                    descriptorAttributes |= PropertyAttributes.Configurable;
                if (attribute.IsWritable)
                    descriptorAttributes |= PropertyAttributes.Writable;
                if (methodGroup.Methods.Count > 1 && methodGroup.PropertyAttributes != descriptorAttributes)
                    throw new InvalidOperationException(string.Format("Inconsistant property attributes detected on {0}.", method));
                methodGroup.PropertyAttributes = descriptorAttributes;
            }

            // Now set the relevant properties on the object.
            foreach (KeyValuePair<string, MethodGroup> pair in functions)
            {
                string name = pair.Key;
                MethodGroup methodGroup = pair.Value;

                // Add the function as a property of the object.
                FastSetProperty(name, new ClrFunction(Engine.Function.InstancePrototype, methodGroup.Methods, name, methodGroup.Length), methodGroup.PropertyAttributes, false);
            }

            PropertyInfo[] properties = type.GetProperties(bindingFlags);
            foreach (PropertyInfo prop in properties)
            {
                var attribute = Attribute.GetCustomAttribute(prop, typeof(JSPropertyAttribute), false) as JSPropertyAttribute;
                if (attribute == null)
                    continue;

                // The property name.
                string name = attribute.Name ?? prop.Name;

                // The property getter.
                ClrFunction getter = null;
                if (prop.CanRead)
                {
                    var getMethod = prop.GetGetMethod(true);
                    getter = new ClrFunction(m_engine.Function.InstancePrototype, new[] { new JSBinderMethod(getMethod, JSFunctionFlags.None) }, name, 0);
                }

                // The property setter.
                ClrFunction setter = null;
                if (prop.CanWrite)
                {
                    var setMethod = prop.GetSetMethod((bindingFlags & BindingFlags.NonPublic) != 0);
                    if (setMethod != null)
                        setter = new ClrFunction(m_engine.Function.InstancePrototype, new[] { new JSBinderMethod(setMethod, JSFunctionFlags.None) }, name, 1);
                }

                // The property attributes.
                var descriptorAttributes = PropertyAttributes.Sealed;
                if (attribute.IsEnumerable)
                    descriptorAttributes |= PropertyAttributes.Enumerable;
                if (attribute.IsConfigurable)
                    descriptorAttributes |= PropertyAttributes.Configurable;

                // Define the property.
                var descriptor = new PropertyDescriptor(getter, setter, descriptorAttributes);
                DefineProperty(name, descriptor, true);
            }
        }

        /// <summary>
        /// Populates the object with properties by searching a .NET type for fields marked with
        /// the [JSField] attribute.  Should be called only once at startup.
        /// </summary>
        internal protected void PopulateFields()
        {
            PopulateFields(null);
        }

        /// <summary>
        /// Populates the object with properties by searching a .NET type for fields marked with
        /// the [JSField] attribute.  Should be called only once at startup.
        /// </summary>
        /// <param name="type"> The type to search for fields. </param>
        internal protected void PopulateFields(Type type)
        {
            if (type == null)
                type = GetType();

            // Find all fields with [JsField]
            foreach (var field in type.GetFields())
            {
                var attribute = (JSFieldAttribute)Attribute.GetCustomAttribute(field, typeof(JSFieldAttribute));
                if (attribute == null)
                    continue;
                FastSetProperty(field.Name, field.GetValue(this), PropertyAttributes.Sealed, false);
            }
        }
    }
}
