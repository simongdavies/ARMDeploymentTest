﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// Azure Management dependencies
using Microsoft.Rest.Azure.Authentication;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var secret = Environment.GetEnvironmentVariable("AZURE_SECRET");
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");

            if (new List<string> { tenantId, clientId, secret, subscriptionId }.Any(i => String.IsNullOrEmpty(i)))
            {
                Console.WriteLine("Please provide ENV vars for AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_SECRET and AZURE_SUBSCRIPTION_ID.");
            }
            else
            {
                RunSample(tenantId, clientId, secret, subscriptionId).Wait();
            }
        }

        public static async Task RunSample(string tenantId, string clientId, string secret, string subscriptionId)
        {
            // Build the service credentials and Azure Resource Manager clients
            var serviceCreds = await ApplicationTokenProvider.LoginSilentAsync(tenantId, clientId, secret);
            var resourceClient = new ResourceManagementClient(serviceCreds);
            resourceClient.SubscriptionId = subscriptionId;

            var resourceGroupName = "sdktestrg";
            var location = "northeurope";
            var templateFileLocation = Path.GetFullPath("template.json");
         

            // Create the resource group
            Write("Creating resource group: {0}", resourceGroupName);
            resourceClient.ResourceGroups.CreateOrUpdate(resourceGroupName, new ResourceGroup { Location = location });
            Write(Environment.NewLine);

            var templateString = File.ReadAllText(Path.GetFullPath("template_params.json"));
            var templateParamsObj = JObject.Parse(templateString)["parameters"];
            var templateParams = templateParamsObj.ToObject<Dictionary<string, Dictionary<string, object>>>();
            Write("{0}", JsonConvert.SerializeObject(templateParams));


            Write("Building the deployment from a json file template from parameters");
            var deployParams = new Deployment
            {
                Properties = new DeploymentProperties
                {
                    Template = JObject.Parse(File.ReadAllText(templateFileLocation)),
                    Parameters = templateParams,
                    Mode = DeploymentMode.Incremental
                }
            };
            
            var sshPubLocation = Path.Combine(@"c:\users",Environment.GetEnvironmentVariable("USERNAME"), ".ssh", "id_rsa.pub");
            if (File.Exists(sshPubLocation))
            {
                Write("Found SSH public key in {0}.", sshPubLocation);
                var pubKey = File.ReadAllText(sshPubLocation);
                Write("Using public key: {0}", pubKey);
                templateParams.Add("sshKeyData", new Dictionary<string, object> { { "value", pubKey } });
            }
            else
            {
                Write("We could not find a RSA public key in {0}. Please create a public key and place it there.", sshPubLocation);
                return;
            }

            Write("Creating deployment named {0} in {1} with template {2}", resourceGroupName, location, templateFileLocation);
            var groupParams = new ResourceGroup { Location = location };
            var deployResult = resourceClient.Deployments.CreateOrUpdate(resourceGroupName, "sample-deploy", deployParams);
            Write(Environment.NewLine);

            Write("Press any key to continue and delete the sample resources");
            Console.ReadLine();

            Write("Deleting resource group {0}", resourceGroupName);
            resourceClient.ResourceGroups.Delete(resourceGroupName);
        }

        private static void Write(string format, params object[] items)
        {
            Console.WriteLine(String.Format(format, items));
        }
    }
}
