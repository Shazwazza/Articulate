using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Examine.Config;
using Umbraco.Core;
using Umbraco.Core.ObjectResolution;
using Umbraco.Web;

namespace Articulate.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                System.Console.WriteLine("ERROR :: Missing folder path to Umbraco install");
                return;
            }

            var umbracoFolder = new DirectoryInfo(args[0]);
            if (!umbracoFolder.Exists)
            {
                System.Console.WriteLine("ERROR :: The umbraco folder specified does not exist");
                return;
            }

            try
            {
                using (var app = new ConsoleApplication(umbracoFolder))
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine("Booting Umbraco...");
                    app.StartApplication();

                    using (var appContext = ApplicationContext.Current)
                    {
                        var canConnect = appContext.DatabaseContext.CanConnect;
                        System.Console.WriteLine();
                        System.Console.WriteLine("Diagnostics:");
                        System.Console.WriteLine("Connection string {0}", appContext.DatabaseContext.ConnectionString);
                        System.Console.WriteLine("Can connect to database? {0}", canConnect);

                        if (canConnect)
                        {
                            var contentCount = appContext.Services.ContentService.Count();
                            System.Console.WriteLine("Count of Umbraco content {0}", contentCount);
                        }

                        System.Console.WriteLine();
                        System.Console.WriteLine("Press any key to exit");
                        System.Console.ReadLine();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("ERROR :: An unhandled exception occurred: {0}", ex);
            }
        }
    }
}
