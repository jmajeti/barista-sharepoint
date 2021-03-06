﻿namespace Barista.SharePoint.Library
{
    using System.IO;
    using Barista.DocumentStore;
    using Barista.Library;
    using Jurassic;
    using Jurassic.Library;
    using Microsoft.SharePoint.Administration;
    using System;
    using System.Linq;

    [Serializable]
    public class SPFarmInstance : ObjectInstance
    {
        [NonSerialized]
        private readonly SPFarm m_farm;

        public SPFarmInstance(ObjectInstance prototype)
            : base(prototype)
        {
            this.PopulateFields();
            this.PopulateFunctions();
        }

        public SPFarmInstance(ObjectInstance prototype, SPFarm farm)
            : this(prototype)
        {
            this.m_farm = farm;
        }

        [JSProperty(Name = "farmManagedAccounts")]
        public SPFarmManagedAccountCollectionInstance FarmManagedAccounts
        {
            get
            {
                return new SPFarmManagedAccountCollectionInstance(this.Engine.Object.InstancePrototype,
                  new SPFarmManagedAccountCollection(SPFarm.Local));
            }
        }

        [JSProperty(Name = "featureDefinitions")]
        public SPFeatureDefinitionCollectionInstance FeatureDefinitions
        {
            get
            {
                return m_farm.FeatureDefinitions == null
                    ? null
                    : new SPFeatureDefinitionCollectionInstance(this.Engine.Object.InstancePrototype, m_farm.FeatureDefinitions);
            }
        }

        [JSProperty(Name = "propertyBag")]
        public HashtableInstance PropertyBag
        {
            get
            {
                return m_farm.Properties == null
                    ? null
                    : new HashtableInstance(this.Engine.Object.InstancePrototype, m_farm.Properties);
            }
        }

        [JSProperty(Name = "servers")]
        public SPServerCollectionInstance Servers
        {
            get { return new SPServerCollectionInstance(this.Engine.Object.InstancePrototype, m_farm.Servers); }
        }

        [JSProperty(Name = "services")]
        public SPServiceCollectionInstance Services
        {
            get { return new SPServiceCollectionInstance(this.Engine.Object.InstancePrototype, m_farm.Services); }
        }

        [JSFunction(Name = "getServiceApplicationById")]
        public object GetServiceApplicationById(object id)
        {
            var guid = GuidInstance.ConvertFromJsObjectToGuid(id);

            foreach (var serviceApplication in m_farm.Services
              .SelectMany(service => service.Applications.Where(serviceApplication => serviceApplication.Id == guid)))
            {
                return new SPServiceApplicationInstance(this.Engine.Object.Prototype, serviceApplication);
            }

            return Null.Value;
        }

        [JSFunction(Name = "extractFarmSolutionByName")]
        public Base64EncodedByteArrayInstance ExtractFarmSolutionByName(string solutionName)
        {
            var solution = m_farm.Solutions.FirstOrDefault(s => s.Name == solutionName);
            if (solution == null)
                return null;

            var fileName = Utilities.GetTempFileName(".wsp");
            solution.SolutionFile.SaveAs(fileName);

            Base64EncodedByteArrayInstance result;
            using (var solutionFile = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var data = solutionFile.ReadToEnd();
                result = new Base64EncodedByteArrayInstance(this.Engine.Object.InstancePrototype, data);
            }

            return result;
        }

        [JSFunction(Name = "getFarmKeyValue")]
        public object GetFarmKeyValueAsObject(string key)
        {
            if (m_farm == null || m_farm.Properties.ContainsKey(key) == false)
                return Undefined.Value;

            string val = Convert.ToString(m_farm.Properties[key]);

            object result;

            //Attempt to convert the string into a JSON Object.
            try
            {
                result = JSONObject.Parse(this.Engine, val, null);
            }
            catch
            {
                result = val;
            }

            return result;
        }

        [JSFunction(Name = "setFarmKeyValue")]
        public void SetFarmKeyValue(string key, object value)
        {
            if (value == null || value == Undefined.Value || value == Null.Value)
                throw new ArgumentNullException("value");

            string stringValue;
            if (value is ObjectInstance)
            {
                stringValue = JSONObject.Stringify(this.Engine, value, null, null);
            }
            else
            {
                stringValue = value.ToString();
            }

            if (m_farm != null)
            {
                if (m_farm.Properties.ContainsKey(key))
                    m_farm.Properties[key] = stringValue;
                else
                    m_farm.Properties.Add(key, stringValue);

                m_farm.Update();
            }
        }
    }
}
