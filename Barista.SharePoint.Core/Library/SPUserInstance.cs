﻿namespace Barista.SharePoint.Library
{
    using System.Reflection;
    using Jurassic;
    using Jurassic.Library;
    using Microsoft.SharePoint;
    using System;

    [Serializable]
    public class SPUserConstructor : ClrFunction
    {
        public SPUserConstructor(ScriptEngine engine)
            : base(engine.Function.InstancePrototype, "SPUser", new SPUserInstance(engine, null))
        {
            this.PopulateFunctions();
        }

        [JSConstructorFunction]
        public SPUserInstance Construct(object arg1)
        {
            if (arg1 is SPUserInstance)
            {
                return new SPUserInstance(this.Engine, (arg1 as SPUserInstance).User);
            }

            var loginName = TypeConverter.ToString(arg1);
            
            SPUser user;
            if (SPHelper.TryGetSPUserFromLoginName(loginName, out user) == false)
            {
                throw new JavaScriptException(this.Engine, "Error", "User cannot be found.");
            }

            return new SPUserInstance(this.Engine, user);
        }

        [JSFunction(Name = "doesUserExist")]
        public bool DoesUserExist(string loginName)
        {
            SPUser user;
            return SPHelper.TryGetSPUserFromLoginName(loginName, out user) && user != null;
        }

    }

    [Serializable]
    public class SPUserInstance : SPPrincipalInstance
    {
        private readonly SPUser m_user;

        public SPUserInstance(ScriptEngine engine, SPUser user)
            : base(new SPPrincipalInstance(engine, user), user)
        {
            this.m_user = user;
            this.PopulateFunctions(this.GetType(), BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        }

        protected SPUserInstance(ObjectInstance prototype, SPUser user)
            : base(prototype, user)
        {
            this.m_user = user;
        }

        internal SPUser User
        {
            get { return m_user; }
        }

        [JSProperty(Name = "alerts")]
        public SPAlertCollectionInstance Alerts
        {
            get
            {
                return m_user.Alerts == null
                    ? null
                    : new SPAlertCollectionInstance(this.Engine.Object.InstancePrototype, m_user.Alerts);
            }
        }

        [JSProperty(Name = "email")]
        public string Email
        {
            get { return m_user.Email; }
        }

        [JSProperty(Name = "groups")]
        public SPGroupCollectionInstance Groups
        {
            get
            {
                return m_user.Groups == null
                  ? null
                  : new SPGroupCollectionInstance(this.Engine.Object.InstancePrototype, m_user.Groups);
            }
        }

        [JSProperty(Name = "isApplicationPrincipal")]
        public bool IsApplicationPrincipal
        {
            get { return m_user.IsApplicationPrincipal; }
        }

        [JSProperty(Name = "isDomainGroup")]
        public bool IsDomainGroup
        {
            get { return m_user.IsDomainGroup; }
        }

        [JSProperty(Name = "isSiteAdmin")]
        public bool IsSiteAdmin
        {
            get { return m_user.IsSiteAdmin; }
        }

        [JSProperty(Name = "isSiteAuditor")]
        public bool IsSiteAuditor
        {
            get { return m_user.IsSiteAuditor; }
        }

        [JSProperty(Name = "notes")]
        public string Notes
        {
            get { return m_user.Notes; }
        }

        [JSProperty(Name = "sid")]
        public string Sid
        {
            get { return m_user.Sid; }
        }

        [JSProperty(Name = "userToken")]
        public SPUserTokenInstance UserToken
        {
            get
            {
                return m_user.UserToken == null
                    ? null
                    : new SPUserTokenInstance(this.Engine.Object.InstancePrototype, m_user.UserToken);
            }
        }

        [JSFunction(Name = "getXml")]
        public string GetXml()
        {
            return m_user.Xml;
        }
    }
}
