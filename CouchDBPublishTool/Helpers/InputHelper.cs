using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2701_NoSQLPublishTool.Helpers
{
    public class InputHelper
    {
        //Constants
        private const string InputString = "input> ";

        /// <summary>
        /// This mehtod waits for user input and handles it afterwards, this should be called on every dead point to keep the program running.
        /// By default the Wait and handle method are looped.
        /// </summary>
        public static void Wait()
        {
            //Get's input and handles it.
            Console.WriteLine();
            Console.Write(InputString);

            //Wait for input.
            string input = Console.ReadLine();

            //Writes an empty line, to seperate any messages from the input request.
            Console.WriteLine();

            //Handles input
            Handle(input);
        }

        /// <summary>
        /// Handle propagates the textual input to the right method.
        /// </summary>
        /// <param name="input"></param>
        public static void Handle(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                //Handles input and calls for new input.
                switch (input.ToLower())
                {
                    case "/help":
                        Program.ShowHelp();
                        break;
                    case "/load":
                        Program.LoadProfile();
                        break;
                    case "/create":
                        Program.CreateProfile();
                        break;
                    case "/confirm-publish":
                        Program.Publish(false, true);
                        break;
                    case "/publish -d":
                        Program.Publish(true);
                        break;
                    case "/publish":
                        Program.Publish();
                        break;
                    case "/backup":
                        Program.Backup();
                        break;
                    case "/add-dd":
                        Program.AddDesignDocument();
                        break;
                    case "/add-env":
                        Program.AddDBEnvironment();
                        break;
                    case "/remove-dd":
                        Program.RemoveDesignDocument();
                        break;
                    case "/info":
                        Program.ShowProfileInfo();
                        break;
                    case "/quit":
                        return;
                    default:
                        Console.WriteLine("Action " + input + " not found!");
                        break;
                }
            }
            Wait();
        }


        /// <summary>
        /// Request the input for an object
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static string RequestInput(string p, string defaultValue = null)
        {
            if (!String.IsNullOrEmpty(defaultValue))
            {
                Console.WriteLine("Hint: leave empty for default value \"" + defaultValue + "\"");
            }
            Console.Write(p.Trim(' ') + " ");

            //Wait for input.
            string retval = Console.ReadLine();

            //Check if default value needs to be set.
            if (String.IsNullOrEmpty(retval))
            {
                retval = defaultValue;
            }
            return retval;
        }
    }
}
