using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoreMoveHubspotData
{
    public static class AppConfiguration
    {
        public static IConfigurationRoot GetConfig()
        {
            string workingDirectory = Environment.CurrentDirectory;
            string projectDirectory = Directory.GetParent(workingDirectory).Parent.FullName;
            var builder = new ConfigurationBuilder()
                                    .SetBasePath(projectDirectory)
                                    .AddJsonFile("appsettings.json");

            var configuration = builder.Build();
            return configuration;
        }
    }
}
