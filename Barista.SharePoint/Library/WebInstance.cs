﻿namespace Barista.SharePoint.Library
{
  using Barista.Extensions;
  using Barista.Library;
  using Jurassic;
  using Jurassic.Library;
  using Microsoft.IdentityModel.Claims;
  using Microsoft.IdentityModel.WindowsTokenService;
  using Newtonsoft.Json;
  using System;
  using System.Collections;
  using System.Linq;
  using System.Net;
  using System.Net.Cache;
  using System.Security.Principal;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using System.Web;
  using System.Web.Caching;
  using System.Xml.Linq;

  [Serializable]
  public class WebInstance : ObjectInstance
  {
    private HttpRequestInstance m_httpRequest;
    private HttpResponseInstance m_httpResponse;

    public WebInstance(ScriptEngine engine)
      : base(engine)
    {
      this.PopulateFields();
      this.PopulateFunctions();
    }

    [JSProperty(Name = "request")]
    public HttpRequestInstance Request
    {
      get
      {
        if (m_httpRequest == null || (m_httpRequest != null && Object.Equals(m_httpRequest.Request, BaristaContext.Current.Request) == false))
        {
          m_httpRequest = new HttpRequestInstance(this.Engine, BaristaContext.Current.Request);
        }

        return m_httpRequest;
      }
      internal set { m_httpRequest = value; }
    }

    [JSProperty(Name = "response")]
    public HttpResponseInstance Response
    {
      get
      {
        if (m_httpResponse == null || (m_httpResponse != null && Object.Equals(m_httpResponse.Response, BaristaContext.Current.Response) == false))
        {
          m_httpResponse = new HttpResponseInstance(this.Engine, BaristaContext.Current.Response);
        }

        return m_httpResponse;
      }
      internal set { m_httpResponse = value; }
    }

    #region Ajax
    [JSFunction(Name = "ajax")]
    public object Ajax(string url, object settings)
    {
      var ajaxSettings = JurassicHelper.Coerce<AjaxSettingsInstance>(this.Engine, settings);

      //If we're running under Claims authentication, impersonate the thread user
      //by calling the Claims to Windows Token Service and call the remote site using
      //the impersonated credentials. NOTE: The Claims to Windows Token Service must be running.
      WindowsImpersonationContext ctxt = null;
      if (Thread.CurrentPrincipal.Identity is ClaimsIdentity)
      {
        IClaimsIdentity identity = (ClaimsIdentity)System.Threading.Thread.CurrentPrincipal.Identity;
        var firstClaim = identity.Claims.FirstOrDefault(c => c.ClaimType == ClaimTypes.Upn);

        if (firstClaim == null)
          throw new InvalidOperationException("No UPN claim found");

        var upn = firstClaim.Value;

        if (String.IsNullOrEmpty(upn))
          throw new InvalidOperationException("A UPN claim was found, however, the value was empty.");

        var currentIdentity = S4UClient.UpnLogon(upn);
        ctxt = currentIdentity.Impersonate();
      }

      try
      {
        var noCachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

        Uri targetUri;
        if (SPHelper.TryCreateWebAbsoluteUri(url, out targetUri) == false)
          throw new InvalidOperationException("Unable to convert target url to absolute uri: " + url);

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(targetUri);
        request.CachePolicy = noCachePolicy; //TODO: Make this configurable.

        //Get the proxy address from the farm property bag setting and use it as the default proxy object.
        var farmProxyAddress = Utilities.GetFarmKeyValue(Constants.FarmProxyAddressKey);

        request.Proxy = String.IsNullOrEmpty(farmProxyAddress) == false
          ? new WebProxy(farmProxyAddress, true, null, CredentialCache.DefaultNetworkCredentials)
          : WebRequest.GetSystemWebProxy();

        if (request.Proxy != null)
        {
          request.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
        }


        if (ajaxSettings != null)
        {
          if (ajaxSettings.UseDefaultCredentials == false)
          {
            if (String.IsNullOrEmpty(ajaxSettings.Username) == false || String.IsNullOrEmpty(ajaxSettings.Password) == false || String.IsNullOrEmpty(ajaxSettings.Domain) == false)
              request.Credentials = new NetworkCredential(ajaxSettings.Username, ajaxSettings.Password, ajaxSettings.Domain);
          }
          else
          {
            request.UseDefaultCredentials = true;
            request.Credentials = CredentialCache.DefaultNetworkCredentials;
          }

          if (String.IsNullOrEmpty(ajaxSettings.Accept))
            request.Accept = ajaxSettings.Accept;

          if (ajaxSettings.Proxy != null)
          {
            var proxySettings = JurassicHelper.Coerce<ProxySettingsInstance>(this.Engine, ajaxSettings.Proxy);

            if (String.IsNullOrEmpty(proxySettings.Address) == false)
            {
              try
              {
                var proxy = new WebProxy(proxySettings.Address, true);
                request.Proxy = proxy;
              }
              catch { /* do nothing */ }
            }

            if (proxySettings.UseDefaultCredentials)
              request.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
          }
        }
        else
        {
          request.Accept = "application/json";
        }

        if (ajaxSettings != null && ajaxSettings.Async)
        {
          var tcs = new TaskCompletionSource<object>();
          try
          {
            request.BeginGetResponse(iar =>
            {
              HttpWebResponse response = null;
              try
              {
                response = (HttpWebResponse)request.EndGetResponse(iar);
                var resultObject = GetResultFromResponse(response);
                tcs.SetResult(resultObject);
              }
              catch (Exception exc) { tcs.SetException(exc); }
              finally { if (response != null) response.Close(); }
            }, null);
          }
          catch (Exception exc) { tcs.SetException(exc); }

          return new DeferredInstance(this.Engine.Object.InstancePrototype, tcs.Task);
        }

        object result;
        try
        {

          var syncResponse = (HttpWebResponse)request.GetResponse();
          result = GetResultFromResponse(syncResponse);
        }
        catch (WebException e)
        {
          //The request failed -- usually a 400
          using (var response = e.Response)
          {
            var httpResponse = (HttpWebResponse)response;
            result = new HttpWebResponseInstance(this.Engine.Object.InstancePrototype, httpResponse);
          }
        }
        catch (Exception ex)
        {
          BaristaDiagnosticsService.Local.LogException(ex, BaristaDiagnosticCategory.Runtime, "Error in web.ajax.call");
          throw;
        }

        return result;
      }
      finally
      {
        if (ctxt != null)
          ctxt.Dispose();
      }
    }

    private object GetResultFromResponse(HttpWebResponse response)
    {
      object resultObject = null;

      if (response.StatusCode == HttpStatusCode.OK)
      {
        using (var stream = response.GetResponseStream())
        {
          byte[] resultData = stream.ToByteArray();
          var result = Encoding.UTF8.GetString(resultData);

          bool success = false;

          //If there is no contents, return undefined.
          if (resultData.Length == 0)
          {
            resultObject = Undefined.Value;
            success = true;
          }

          //Attempt to convert the result into Json
          if (!success)
          {
            try
            {
              resultObject = JSONObject.Parse(this.Engine, result, null);
              success = true;
            }
            catch { /* Do Nothing. */ }
          }

          if (!success)
          {
            //Attempt to convert the result into Xml
            try
            {
              XDocument doc = XDocument.Parse(result);
              var jsonDocument = JsonConvert.SerializeXmlNode(doc.Root.GetXmlNode());
              resultObject = JSONObject.Parse(this.Engine, jsonDocument, null);
              success = true;
            }
            catch { /* Do Nothing. */ }
          }

          if (!success)
          {
            //If we couldn't convert as json or xml, use it as a byte array.
            resultObject = new Base64EncodedByteArrayInstance(this.Engine.Object.InstancePrototype, resultData);
          }
        }
      }
      else
      {
        resultObject = String.Format("Error attempting to retrieve {2}: {0} {1}", response.StatusCode, response.StatusDescription, response.ResponseUri);
      }

      return resultObject;
    }
    #endregion

    [JSFunction(Name = "parseQueryString")]
    public object ParseQueryString(object query)
    {
      if ((query is string) == false)
        return Null.Value;

      var result = this.Engine.Object.Construct();
      var dict = HttpUtility.ParseQueryString(query as string);
      foreach (var key in dict.AllKeys)
      {
        result.SetPropertyValue(key, dict[key], false);
      }
      return result;
    }

    [JSFunction(Name = "getItemFromCache")]
    public object GetItemFromCache(string cacheKey)
    {
      var result = HttpRuntime.Cache.Get(cacheKey) as string;
      if (result == null)
        return Null.Value;

      return result;
    }

    [JSFunction(Name = "addItemToCache")]
    public void AddItemToCache(string cacheKey, object item, object absoluteExpiration, object slidingExpiration)
    {
      string stringItem;
      DateTime dtExpiration = Cache.NoAbsoluteExpiration;
      TimeSpan tsExpiration = Cache.NoSlidingExpiration;

      if (item == Null.Value || item == Undefined.Value || item == null)
        return;

      if (item is ObjectInstance)
        stringItem = JSONObject.Stringify(this.Engine, item, null, null);
      else
        stringItem = item.ToString();

      if (absoluteExpiration != Null.Value && absoluteExpiration != Undefined.Value && absoluteExpiration != null)
      {
        if (absoluteExpiration is DateInstance)
          dtExpiration = DateTime.Parse((absoluteExpiration as DateInstance).ToISOString());
        else
          dtExpiration = DateTime.Parse(absoluteExpiration.ToString());
      }

      if (slidingExpiration != Null.Value && slidingExpiration != Undefined.Value && slidingExpiration != null)
      {
        if (slidingExpiration is DateInstance)
          tsExpiration = TimeSpan.FromMilliseconds((slidingExpiration as DateInstance).ValueInMilliseconds);
        else
          tsExpiration = TimeSpan.Parse(slidingExpiration.ToString());
      }

      HttpRuntime.Cache.Insert(cacheKey, stringItem, null, dtExpiration, tsExpiration);
    }

    [JSFunction(Name = "removeItemFromCache")]
    public string RemoveItemFromCache(string cacheKey)
    {
      var result = HttpRuntime.Cache.Remove(cacheKey) as string;
      return result;
    }

    [JSFunction(Name = "getItemsInCache")]
    public object GetItemsInCache()
    {
      var result = this.Engine.Object.Construct();
      foreach (var item in HttpRuntime.Cache.OfType<DictionaryEntry>())
      {
        var key = item.Key as string;
        if (key != null)
        {
          string value = item.Value as string;

          if (value == null)
            result.SetPropertyValue(key, Null.Value, false);
          else
            result.SetPropertyValue(key, value, false);
        }
        
      }
      return result;
    }
  }
}
