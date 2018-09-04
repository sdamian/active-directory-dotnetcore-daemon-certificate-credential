/*
 The MIT License (MIT)

Copyright (c) 2015 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

// The following using statements were added for this sample.
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace TodoListDaemonWithCert
{
    static class Program
    {
        private static AuthenticationConfig config;
        private static HttpClient httpClient = new HttpClient();
        private static AuthenticationContext authContext = null;
        private static ClientAssertionCertificate certCred = null;

        private static int errorCode;

        static int Main(string[] args)
        {
            // Return code so that exceptions provoke a non-null return code for the daemon
            errorCode = 0;

            // Create the authentication context to be used to acquire tokens.
            config = AuthenticationConfig.ReadFromJsonFile("appsettings.json");
            authContext = new AuthenticationContext(config.Authority);

            // Initialize the Certificate Credential to be used by ADAL.
            X509Certificate2 cert = ReadCertificate();
            if (cert == null)
            {
                Console.WriteLine($"Cannot find active certificate '{config.CertName}' in certificates for current user. Please check configuration");
                return -1;
            }

            // Then create the certificate credential client assertion.
            certCred = new ClientAssertionCertificate(config.ClientId, cert);

            // Call the ToDoList service 10 times with short delay between calls.
            DisplayTodoList().Wait();
            return errorCode;
        }


        /// <summary>
        /// Reads the certificate
        /// </summary>
        private static X509Certificate2 ReadCertificate()
        {
           ///var cert = new X509Certificate2("./key.pfx", "qwerty");
           var cert = new X509Certificate2("./hub.pfx", "P@ssw0rd");
            return cert;
        }


        /// <summary>
        /// Get an access token from Azure AD using client credentials.
        /// If the attempt to get a token fails because the server is unavailable, retry twice after 3 seconds each
        /// </summary>
        private static async Task<AuthenticationResult> GetAccessToken(string todoListResourceId)
        {
            AuthenticationResult result = null;
            int retryCount = 0;
            bool retry = false;

            do
            {
                retry = false;
                errorCode = 0;

                try
                {   // ADAL includes an in-memory cache, so this call will only send a message to the server if the cached token is expired.
                    result = await authContext.AcquireTokenAsync(todoListResourceId, certCred);
                }
                catch (AdalException ex)
                {
                    if (ex.ErrorCode == "temporarily_unavailable")
                    {
                        retry = true;
                        retryCount++;
                        Thread.Sleep(3000);
                    }

                    Console.WriteLine(
                        String.Format("An error occurred while acquiring a token\nTime: {0}\nError: {1}\nRetry: {2}\n",
                        DateTime.Now.ToString(),
                        ex.ToString(),
                        retry.ToString()));

                    errorCode = -1;
                }

            } while ((retry == true) && (retryCount < 3));
            return result;
        }

   
        /// <summary>
        /// Display the list of todo items by querying the todolist service
        /// </summary>
        /// <returns></returns>
        static async Task DisplayTodoList()
        {
            AuthenticationResult result = await GetAccessToken("https://maxcode.sharepoint.com");
            // Add the access token to the authorization header of the request.
        
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Call the To Do list service.
            HttpResponseMessage response = await httpClient.GetAsync("https://maxcode.sharepoint.com/timesheet/_api/web/lists/getByTitle('Timesheet')/items");
            
            if (response.IsSuccessStatusCode)
            {
                // Read the response and output it to the console.
                string s = await response.Content.ReadAsStringAsync();
                Console.WriteLine(s);
            }
            else
            {
                Console.WriteLine("Failed to retrieve To Do list\nError:  {0}\n", response.ReasonPhrase);
            }
        }
    }
}
