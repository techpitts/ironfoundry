﻿namespace CloudFoundry.Net.Vmc
{
    using System;
    using System.Linq;
    using System.Net;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using RestSharp;

    public abstract class VcapRequestBase
    {
        private static readonly ushort[] VMC_HTTP_ERROR_CODES =
        {
            (ushort)HttpStatusCode.BadRequest,          // 400
            (ushort)HttpStatusCode.Forbidden,           // 403
            (ushort)HttpStatusCode.NotFound,            // 404
            (ushort)HttpStatusCode.MethodNotAllowed,    // 405
            (ushort)HttpStatusCode.InternalServerError, // 500
        };

        private static readonly string[] argFormats = new[]
            {
                null,              // 0
                "{0}",             // 1
                "{0}/{1}",         // 2
                "{0}/{1}/{2}",     // 3
                "{0}/{1}/{2}/{3}", // 4
            };

        protected readonly VcapCredentialManager credentialManager;
        protected readonly RestClient client;
        protected RestRequest request;

        protected VcapRequestBase(VcapCredentialManager credentialManager)
        {
            this.credentialManager = credentialManager;
            client = BuildClient();
        }

        protected VcapRequestBase(VcapCredentialManager credentialManager, bool useAuthentication, Uri uri = null)
        {
            this.credentialManager = credentialManager;
            client = BuildClient(useAuthentication, uri);
        }

        public RestResponse Execute()
        {
            return ExecuteRequest(client, request);
        }

        public TResponse Execute<TResponse>()
        {
            return ExecuteRequest<TResponse>(client, request);
        }

        protected RestRequest BuildRequest(Method argMethod, params object[] args)
        {
            var rv = new RestRequest
            {
                Method = argMethod,
            };
            return ProcessRequestArgs(rv, args);
        }

        protected RestRequest BuildRequest(Method argMethod, DataFormat argFormat, params object[] args)
        {
            var rv = new RestRequest
            {
                Method = argMethod,
                RequestFormat = argFormat,
            };
            return ProcessRequestArgs(rv, args);
        }

        private RestResponse ExecuteRequest(RestClient argClient, RestRequest argRequest)
        {
            RestResponse response = argClient.Execute(argRequest);
            ProcessResponse(response);
            return response;
        }

        private TResponse ExecuteRequest<TResponse>(RestClient argClient, RestRequest argRequest)
        {
            RestResponse response = argClient.Execute(argRequest);
            ProcessResponse(response);
            if (response.Content.IsNullOrWhiteSpace())
            {
                return default(TResponse);
            }
            else
            {
                return JsonConvert.DeserializeObject<TResponse>(response.Content);
            }
        }

        private RestClient BuildClient()
        {
            return BuildClient(true);
        }

        private RestClient BuildClient(bool argUseAuth, Uri argUri = null)
        {
            string baseUrl = credentialManager.CurrentTarget.AbsoluteUri;
            if (null != argUri)
            {
                baseUrl = argUri.AbsoluteUri;
            }

            var rv = new RestClient
            {
                BaseUrl = baseUrl,
                FollowRedirects = false,
            };

            if (argUseAuth && credentialManager.HasToken)
            {
                rv.AddDefaultHeader("AUTHORIZATION", credentialManager.CurrentToken);
            }

            return rv;
        }

        private static RestRequest ProcessRequestArgs(RestRequest argRequest, params object[] args)
        {
            if (null == argRequest)
            {
                throw new ArgumentNullException("argRequest");
            }
            if (false == args.IsNullOrEmpty())
            {
                if (args.Length > argFormats.Length)
                {
                    throw new InvalidOperationException();
                }
                argRequest.Resource = String.Format(argFormats[args.Length], args);
            }
            return argRequest;
        }

        private static void ProcessResponse(RestResponse argResponse)
        {
            // TODO process error codes!
            if (VMC_HTTP_ERROR_CODES.Contains((ushort)argResponse.StatusCode))
            {
                string errorMessage;

                JObject parsed = JObject.Parse(argResponse.Content);
                JToken codeToken;
                JToken descToken;
                if (parsed.TryGetValue("code", out codeToken) && parsed.TryGetValue("description", out descToken))
                {
                    errorMessage = String.Format("Error {0}: {1}", codeToken, descToken);
                }
                else
                {
                    errorMessage = String.Format("Error (HTTP {0}): {1}", argResponse.StatusCode, argResponse.Content);
                }

                if (argResponse.StatusCode == HttpStatusCode.BadRequest ||
                    argResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new VmcNotFoundException(errorMessage);
                }
                else
                {
                    throw new VmcTargetException(errorMessage);
                }
            }
        }
    }

    public class VcapRequest : VcapRequestBase
    {
        public VcapRequest(VcapCredentialManager credMgr, params object[] resourceParams)
            : this(credMgr, true, null, resourceParams) { }

        public VcapRequest(VcapCredentialManager credMgr, bool useAuth, Uri uri, params object[] resourceParams)
            : base(credMgr, useAuth, uri)
        {
            request = BuildRequest(Method.GET, resourceParams);
        }
    }

    public class VcapJsonRequest : VcapRequestBase
    {
        public VcapJsonRequest(VcapCredentialManager credentialManager, Method method, params object[] resourceParams)
            : this(credentialManager, method, null, resourceParams) { }

        public VcapJsonRequest(VcapCredentialManager credentialManager, Method method, object body, params object[] resourceParams)
            : base(credentialManager)
        {
            request = BuildRequest(method, DataFormat.Json, resourceParams);
            if (null != body)
            {
                request.AddBody(body);
            }
        }

        public void AddParameter(string param, object value)
        {
            this.request.AddParameter(param, value);
        }

        public void AddFile(string name, string path)
        {
            this.request.AddFile(name, path);
        }
    }
}