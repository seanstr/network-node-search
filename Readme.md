#README

##Note
This package is to showcase code only.  It is not intended to be runnable, and any sensitive information has been removed or changed.
##Pre setup
* Ensure that a ConnectionString.asp file is created. Use ConnectionString.asp.tmpl as a template
* Ensure the Microsoft SDK is installed on the web server. This is required in order to run Step 4 below

##Development
* Create a SQL Server alias called *clli* that points to your local sql server instance that you would like to use
* Ensure that the protocol you select is enabled under **SQL Native Client Configuration** as well as **SQL Server Network Configuration**
* Test the alias by logging into the *clli* server using SQL Server Management Studio
* Ensure Nuget Package restore is enabled, right click on the solution and select Enable Nuget Package Restore

##Set up
In order to set up on a new server follow these steps:

1. Build the Network.Node.Session project
2. Copy the dll to the server you want to deploy on
3. Run `regasm Network.Node.Session.dll`
	* Ensure the 32 bit version is run
	* Ensure this is run as an administrator
	* regasm is in C:\Windows\Microsoft.NET\Framework\v4.0.30319
4. Run `gacutil /i Network.Node.Session.dll`
	* Ensure this is run as an administrator
	* gacutil is in C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools or download SDKTools
5. If you have an existing Session server set up skip this step
    1. Run the following command `aspnet_regsql -ssadd -sstype p -S (server) <-E | -U (login) -P (password>` 
using either your current credentials (using the `-E` flag) or use specified credentials with the `-U` and `-P` flags.
    2. Ensure there is a database called __ASPState__ that has been created
    3. Also ensure that __SQL Server Agent__ windows service is running. This will ensure expired sessions are cleared from the database
5. Edit the 32 bit machine.config file typically located in `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\Config`
6. Add a connection string entry to the `connectionStrings` node
7. It should look like this: `<add name="Session" connectionString="Server=localhost\sqlexpress;Database=aspstate;Trusted_Connection=True;"/>`
Substituting the connection information for the session server you are using

##Deployment
There are 2 publishing profiles in the Network.Node.Web project: Dev and Prod. Dev publishes to the dev iis server setting up the required connection string settings
in web.config. Change the Dev.pubxml file if the connection strings or servers change. The Prod publishing profile publishes to a deployment package. This
package should be run either on the server you plan to deploy on or using the command line and following the readme file that is generated alongside the deployment
package.

##Troubleshooting
* Ensure Application pool in IIS is setup to enable 32 bit applications
	* This setting can be found under __Advanced Settings__ in IIS
* Ensure [Entity Framework 6 Tools for Visual Studio](http://www.microsoft.com/en-us/download/confirmation.aspx?id=40762) is installed
	* If not you may see errors regarding the edmx file or other Entity Framework related errors
* In order to enable IE support, ensure IIS Response Headers has an entry to send the `X-UA-Compatible` header with a value of `IE=10`