﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18408
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Barista.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Barista.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /*
        ///string.js - Copyright (C) 2012-2013, JP Richardson &lt;jprichardson@gmail.com&gt;
        ///*/
        ///(function() {
        ///  &quot;use strict&quot;;
        ///
        ///  var VERSION = &apos;1.3.0&apos;;
        ///
        ///  var ENTITIES = {};
        ///
        ///  function S(s) {
        ///    if (s !== null &amp;&amp; s !== undefined) {
        ///      if (typeof s === &apos;string&apos;)
        ///        this.s = s;
        ///      else
        ///        this.s = s.toString();
        ///    } else {
        ///      this.s = s; //null or undefined
        ///    }
        ///
        ///    this.orig = s; //original object, currently only used by toCSV() and toBoolean()
        ///
        ///    if (s !== null &amp;&amp; s !== undefined) {
        ///      if (th [rest of string was truncated]&quot;;.
        /// </summary>
        public static string _string {
            get {
                return ResourceManager.GetString("_string", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to //source: http://johnkalberer.com/2011/08/24/automapper-in-javascript/
        ///var automapper = (function (app) {
        ///    if (app.automapper) {
        ///        return app.automapper;
        ///    }
        ///
        ///    var dictionary = {};
        ///
        ///    app.automapper = {
        ///        createMap: function (sourceKey, destinationKey) {
        ///            var combinedKey = sourceKey + destinationKey, functions;
        ///            dictionary[combinedKey] = {};
        ///
        ///            functions = {
        ///                forMember: function (key, e) {
        ///                    dictionary[comb [rest of string was truncated]&quot;;.
        /// </summary>
        public static string Automapper {
            get {
                return ResourceManager.GetString("Automapper", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to //  Chance.js 0.3.3
        /////  http://chancejs.com
        /////  (c) 2013 Victor Quinn
        /////  Chance may be freely distributed or modified under the MIT license.
        ///
        ///(function (that) {
        ///
        ///    // Constructor
        ///    var Chance = function (seed) {
        ///        if (seed !== undefined) {
        ///            this.seed = seed;
        ///        }
        ///        this.mt = this.mersenne_twister(seed);
        ///    };
        ///
        ///    // Wrap the MersenneTwister
        ///    Chance.prototype.random = function () {
        ///        return this.mt.random(this.seed);
        ///    };
        ///
        ///    // -- Basics --        /// [rest of string was truncated]&quot;;.
        /// </summary>
        public static string chance {
            get {
                return ResourceManager.GetString("chance", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // Author: Michael Schøler, 2008
        ///// Dual licensed as MIT and LGPL, use as you like, don&apos;t hold me responsible for success or failure though
        ///Array.prototype.compareTo = function (compareAry) {
        ///    if (this.length === compareAry.length) {
        ///        var i;
        ///        for (i = 0; i &lt; compareAry.length; i += 1) {
        ///            if (Object.prototype.toString.call(this[i]) === &apos;[object Array]&apos;) {
        ///                if (this[i].compareTo(compareAry[i]) === false) {
        ///                    return false;
        ///                }        /// [rest of string was truncated]&quot;;.
        /// </summary>
        public static string jsonDataHandler {
            get {
                return ResourceManager.GetString("jsonDataHandler", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /*--------------------------------------------------------------------------
        ///* linq.js - LINQ for JavaScript
        ///* ver 2.2.0.2 (Jan. 21th, 2011)
        ///*
        ///* created and maintained by neuecc &lt;ils@neue.cc&gt;
        ///* licensed under Microsoft Public License(Ms-PL)
        ///* http://neue.cc/
        ///* http://linqjs.codeplex.com/
        ///*--------------------------------------------------------------------------*/
        ///
        ///Enumerable = (function ()
        ///{
        ///    var Enumerable = function (getEnumerator)
        ///    {
        ///        this.GetEnumerator = getEnumerator;
        ///    } [rest of string was truncated]&quot;;.
        /// </summary>
        public static string linq {
            get {
                return ResourceManager.GetString("linq", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // moment.js
        ///// version : 1.7.2
        ///// author : Tim Wood
        ///// license : MIT
        ///// momentjs.com
        ///(function(a){function E(a,b,c,d){var e=c.lang();return e[a].call?e[a](c,d):e[a][b]}function F(a,b){return function(c){return K(a.call(this,c),b)}}function G(a){return function(b){var c=a.call(this,b);return c+this.lang().ordinal(c)}}function H(a,b,c){this._d=a,this._isUTC=!!b,this._a=a._a||null,this._lang=c||!1}function I(a){var b=this._data={},c=a.years||a.y||0,d=a.months||a.M||0,e=a.weeks||a.w||0,f=a.days||a.d||0,g=a.hou [rest of string was truncated]&quot;;.
        /// </summary>
        public static string moment_min {
            get {
                return ResourceManager.GetString("moment_min", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to (function(b){var a={VERSION:&quot;2.2.0&quot;,Result:{SUCCEEDED:1,NOTRANSITION:2,CANCELLED:3,ASYNC:4},Error:{INVALID_TRANSITION:100,PENDING_TRANSITION:200,INVALID_CALLBACK:300},WILDCARD:&quot;*&quot;,ASYNC:&quot;async&quot;,create:function(g,h){var j=(typeof g.initial==&quot;string&quot;)?{state:g.initial}:g.initial;var f=h||g.target||{};var l=g.events||[];var i=g.callbacks||{};var d={};var k=function(m){var p=(m.from instanceof Array)?m.from:(m.from?[m.from]:[a.WILDCARD]);d[m.name]=d[m.name]||{};for(var o=0;o&lt;p.length;o++){d[m.name][p[o]]=m.to|| [rest of string was truncated]&quot;;.
        /// </summary>
        public static string stateMachine {
            get {
                return ResourceManager.GetString("stateMachine", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /*
        /// *  Sugar Library v1.3.9
        /// *
        /// *  Freely distributable and licensed under the MIT-style license.
        /// *  Copyright (c) 2013 Andrew Plummer
        /// *  http://sugarjs.com/
        /// *
        /// * ---------------------------- */
        ///(function(){var k=true,l=null,n=false;function aa(a){return function(){return a}}var p=Object,q=Array,r=RegExp,s=Date,t=String,u=Number,v=Math,ba=p.prototype.toString,ca=typeof global!==&quot;undefined&quot;?global:this,da={},ea=p.defineProperty&amp;&amp;p.defineProperties,x=&quot;Array,Boolean,Date,Function,Number,String,RegExp&quot;.split [rest of string was truncated]&quot;;.
        /// </summary>
        public static string sugar_1_3_9_custom_min {
            get {
                return ResourceManager.GetString("sugar_1_3_9_custom_min", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        public static byte[] uaregexes {
            get {
                object obj = ResourceManager.GetObject("uaregexes", resourceCulture);
                return ((byte[])(obj));
            }
        }
    }
}
