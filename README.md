## System Overview 
This is the code base, project and references for Tucson 
Tucson is a C# wrapper dll for MongoDB that allows for factory pattern
implementation to access MongoDB.  Tucson allows you to define document based schemas to use with mongo, query,update,delete mongo as well. There are noadministrative task that you can issue provided in code base.

##Getting started
You will need to compile code and create Tucson.Core.dll. You will then include this in your code and build from it.  Since mongo docuemnts always have a key you will see Interfaces for for Keyed Repository, Keyed Entity, etc. 

There will always implement the key (ObjectID) for your code. 

Once you have defined an Entity(Mongo Document) a repository, a map, and a service to intialize your repository and map...you will be ready to start using mongo. 

There are a few featured that I have commented out and will later visit or feel free to do a pull request. There is another extension for EFClient that I will be providing soon.
