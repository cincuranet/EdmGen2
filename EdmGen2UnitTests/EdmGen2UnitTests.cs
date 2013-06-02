using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;
using EdmGen2;
using System.Xml.Linq;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using Microsoft.VisualBasic;

namespace EdmGen2UnitTests
{
    [TestClass]
    public class EdmGen2UnitTests
    {
        public EdmGen2UnitTests()
        {
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        private DirectoryInfo TestDataFilesDirectory
        {
            get { return new DirectoryInfo(@"..\..\..\EdmGen2UnitTests\TestDataFiles"); }
        }

        private DirectoryInfo EdmGen2ExeDirectory
        {
            get { return new DirectoryInfo(@"..\..\..\EdmGen2\bin\Debug\EdmGen2.exe"); }
        }

        private string ProviderName
        {
            get { return "System.Data.SqlClient"; }
        }

        private string ConnectionString
        {
            get { return "Data Source=mkaufman-dev2;Initial Catalog=Northwind;Integrated Security=True;MultipleActiveResultSets=True"; }
        }

        private bool CompareXMLFiles(string f1, string f2)
        {
            // load the files into xdocuments to strip out all insignificant whitespace
            XDocument x1 = XDocument.Load(f1, LoadOptions.None);
            XDocument x2 = XDocument.Load(f2, LoadOptions.None);

            StringWriter sw1 = new StringWriter();
            StringWriter sw2 = new StringWriter();

            x1.Root.Save(sw1);
            x2.Root.Save(sw2);

            File.WriteAllText("sw1.txt", sw1.ToString());
            File.WriteAllText("sw2.txt", sw2.ToString());

            return sw1.ToString().Equals(sw2.ToString());
        }

        private bool CompareFiles(string f1, string f2)
        {
            String s1 = File.ReadAllText(f1);
            String s2 = File.ReadAllText(f2);
            return s1.Equals(s2);
        }

        //Console.WriteLine("                 /ModelGen <connection string> <provider name> <model name>");
        //Console.WriteLine("                 /RetrofitModel <connection string> <provider name> <model name> <percent threshold>?");
        //Console.WriteLine("                 /ViewGen cs|vb <edmx file>");
        //Console.WriteLine("                 /Validate <edmx file>");


        [TestMethod]
        public void FromEdmxV1()
        {
            FromEdmxToEdmx("Northwind_v35");
        }

        [TestMethod]
        public void FromEdmxV2()
        {
            FromEdmxToEdmx("Northwind_v40");
        }

        private void FromEdmxToEdmx(string edmxFileNameSansExtensions)
        {
            // first, split an existing edmx apart
            string edmxName = edmxFileNameSansExtensions + ".edmx";
            string csdlName = edmxFileNameSansExtensions + ".csdl";
            string mslName = edmxFileNameSansExtensions + ".msl";
            string ssdlName = edmxFileNameSansExtensions + ".ssdl";

            string originalEdmx = Path.Combine(TestDataFilesDirectory.FullName, edmxName);
            string args = "/FromEdmx " + originalEdmx;

            EdmGen2.EdmGen2.Main(args.Split(' '));

            // next, combine the pieces into a new edmx
            string[] args2 = { "/ToEdmx", csdlName, mslName, ssdlName };
            EdmGen2.EdmGen2.Main(args2);

            if (!CompareXMLFiles(originalEdmx, edmxName))
            {
                throw new Exception("Test failed!  edmx files didn't compare");
            }
        }


        [TestMethod]
        public void ModelGen_V1()
        {
            ModelGenTest("1.0", false);
        }

        [TestMethod]
        public void ModelGen_V2_FKs()
        {
            ModelGenTest("2.0", true);

        }

        [TestMethod]
        public void ModelGen_V2_NoFKs()
        {
            ModelGenTest("2.0", false);
        }

    private void ModelGenTest(string version, bool includeFKs)
    {
            string modelName = "Northwind";
            string edmxFileName = modelName + ".edmx";
            string csdlFileName = modelName + ".csdl";
            string ssdlFileName = modelName + ".ssdl";
            string mslFileName = modelName + ".msl";


            string[] args = { "/ModelGen", ConnectionString, ProviderName, modelName, version };
            if ( includeFKs )
            {
                string[] tmp = new string[args.Length + 1];
                System.Array.Copy(args, tmp, args.Length);
                tmp[args.Length] = "includeFKs";
                args = tmp;
            }

            EdmGen2.EdmGen2.Main(args);

            // make a copy of the file so it doesnt' get overwritten
            if (File.Exists(edmxFileName + ".original"))
            {
                File.Delete(edmxFileName + ".original");
            }

            File.Copy(edmxFileName, edmxFileName + ".original");

            // split edmx apart
            string[] args2 = { "/FromEdmx", edmxFileName };
            EdmGen2.EdmGen2.Main(args2);

            // delete the original one created
            File.Delete(edmxFileName);

            // combine edmx
            string[] args3 = { "/ToEdmx", csdlFileName, mslFileName, ssdlFileName };
            EdmGen2.EdmGen2.Main(args3);

            if (!CompareXMLFiles(edmxFileName + ".original", edmxFileName))
            {
                throw new Exception("Test failed!  edmx files didn't compare");
            }
    }

        [TestMethod]
        public void ViewGenV1_CS()
        {
            ViewGenTest("Northwind_v35.edmx", "cs");
        }

        [TestMethod]
        public void ViewGenV2_CS()
        {
            ViewGenTest("Northwind_v40.edmx", "cs");
        }

        [TestMethod]
        public void ViewGenV1_VB()
        {
            ViewGenTest("Northwind_v35.edmx", "vb");
        }

        [TestMethod]
        public void ViewGenV2_VB()
        {
            ViewGenTest("Northwind_v40.edmx", "vb");
        }

        [TestMethod]
        public void CodeGenV1_CS()
        {
            CodeGenTest("Northwind_v35.edmx", "cs");
        }

        [TestMethod]
        public void CodeGenV2_CS()
        {
            CodeGenTest("Northwind_v40.edmx", "cs");
        }

        [TestMethod]
        public void CodeGenV1_VB()
        {
            CodeGenTest("Northwind_v35.edmx", "vb");
        }

        [TestMethod]
        public void CodeGenV2_VB()
        {
            CodeGenTest("Northwind_v40.edmx", "vb");
        }

        private void CodeGenTest(string edmxFileName, string languageOption)
        {
            string originalEdmx = Path.Combine(TestDataFilesDirectory.FullName, edmxFileName);
            string[] args = { "/CodeGen", languageOption, originalEdmx };
            EdmGen2.EdmGen2.Main(args);
            string codeFileName = edmxFileName.Substring(0, edmxFileName.Length - ".edmx".Length) + "." + languageOption;
            VerifyCodeFileCompiles(codeFileName, languageOption);
        }

        private void ViewGenTest(string edmxFileName, string languageOption)
        {
            string originalEdmx = Path.Combine(TestDataFilesDirectory.FullName, edmxFileName);
            string[] args = { "/ViewGen", languageOption, originalEdmx };
            EdmGen2.EdmGen2.Main(args);
            string codeFileName = edmxFileName.Substring(0, edmxFileName.Length - ".edmx".Length) + ".GeneratedViews." + languageOption;
            VerifyCodeFileCompiles(codeFileName, languageOption);
        }

        private void VerifyCodeFileCompiles(string codeFileName, string languageOption)
        {
            string codeString = File.ReadAllText(codeFileName);

            // get correct code dom provider for language options
            CodeDomProvider codeProvider = null;
            if (languageOption == "cs")
            {
                codeProvider = new CSharpCodeProvider();
            }
            else if (languageOption == "vb")
            {
                codeProvider = new VBCodeProvider();
            }
            else
            {
                throw new ArgumentException("Unsupported value.  LanguageOpetion must be 'vb' or 'cs'", "languageOption");
            }

            CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.CompilerOptions = "/t:library /r:System.Data.dll /r:System.Data.Entity.dll /r:System.Runtime.Serialization.dll /r:System.Core.dll /r:System.Xml.dll /r:System.dll";
            CompilerResults results = codeProvider.CompileAssemblyFromSource(parameters, codeString);

            // throw exception if any errors occurred. 
            if (results.Errors.HasErrors)
            {
                throw new Exception("Generted code didn't compile");
            }
        }
    }
}
