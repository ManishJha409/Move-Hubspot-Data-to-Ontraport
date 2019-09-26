using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoveHubspotToOntraport;
using System;
using System.IO;

namespace CoreMoveHubspotData
{
    class Program
    {
        static void Main(string[] args)
        {      
            Common common = new Common();
            Hubspot hub = new Hubspot();
            Console.WriteLine("Process Started");
            ErrorLog.InfoMessage("Process Started");

            hub.MoveContactAsync();

            hub.MoveDealAsync();

            Console.WriteLine("Process Completed");
            ErrorLog.InfoMessage("Process Completed");

            Console.ReadKey();
        }
    }
}
