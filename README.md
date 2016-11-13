# SSDP UPnP v2.0 Library


[![NuGet Badge](https://buildstats.info/nuget/SSDP.UPnP.PCL)](https://www.nuget.org/packages/RSSDP/)

[![.NET Standard](http://img.shields.io/badge/.NET_Standard-v1.2-green.svg)](https://docs.microsoft.com/da-dk/dotnet/articles/standard/library) [![System.Reactive](http://img.shields.io/badge/Rx-v3.1.0-ff69b4.svg)](http://reactivex.io/) 

[![UPnP](http://img.shields.io/badge/UPnP_Device_Architecture-v2.0-blue.svg)](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)

## Why This Library
There are other SSDP Libraries available, so why this library?

This library is created for [Reactive Extensions](http://reactivex.io/ "Reactive Extensions"). As SSDP deals with a stream of messages continuously coming in, Rx IMHO provides a much more elegant programming paradigm than what exists already. Sure the Rx learning curve can feel a bit steep at fist, but it is worth the effort.

This library is created for .NET Standard 1.2 making it modern and ready for the future. Also, the library seeks to balance broad compatibility with simplicity by supporting only the most recent platforms - i.e. iOS, Android, UWP, .NET Core 1.0+ and .NET 4.5.1+ and Mono. So no support for older versions of Windows Phone or Silver Light for this library.

This project is based on [SocketLite.PCL](https://github.com/1iveowl/SocketLite.PCL) for cross platform TCP sockets support, that uses the "Bait and Switch" pattern. To read about "Bait and Switch" I can recoomend reading this great short blog post: [The Bait and Switch PCL Trick](http://log.paulbetts.org/the-bait-and-switch-pcl-trick/).
## Getting Started is Easy

#### Using Statements
Using Statements assumed in the following code examples:
```csharp
using ISimpleHttpServer.Service;
using SimpleHttpServer.Service;

using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Service;

using SSDP.Console.Test.NET.Model;
using SSDP.UPnP.PCL.Service;
```

#### Listening for UPnP Traffic
The SSDP Library needs a HttpListener which is to be dependency injected into the Device and/or ControlPoint instances later. For a better understanding what a Device and a ControlPoint is in a UPnP context see the [UPnP Architecture documentation](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf). 
```csharp
IHttpListener HttpListener = new HttpListener(timeout:TimeSpan.FromSeconds(30));

// Get an available network interface
var comm = new CommunicationsInterface(); //
var allComms = comm.GetAllInterfaces();
var networkComm = allComms.FirstOrDefault(x => x.GatewayAddress != null);
```
Make the HttpListener start listening to the relevant ports as specified in the  [UPnP Architecture documentation](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)). 

```csharp
await HttpListener.StartTcpRequestListener(1900, networkComm); 
await HttpListener.StartTcpResponseListener(1901, networkComm);
await HttpListener.StartUdpMulticastListener("239.255.255.250", 1900, networkComm);
await HttpListener.StartUdpListener(1900, networkComm);
```
> Important Note: On Windows you might want to stop the "SSDP Service". My experience is that if you don't stop the SSDP Windows Service, the service will intercept the UPnP multicasts and consequently nothing will get through to the HttpListener.

#### Creating a UPnP Device
Create a UPnP Device and start listen to M-SEARCH Requests like this:
```csharp
device = new Device(HttpListener);

var MSearchRequestSubscribe = device.MSearchObservable.Subscribe(
	req =>
	{
		// Insert your code here for dealing with the incoming request.
	}
```
#### Creating a UPnP ControlPoint
Create a UPnP ControlPoint and start listen to M-SEARCH Responses and NOTIFY messages like this:
```csharp
controlPoint = new ControlPoint(HttpListener);
    
var MSearchresponseSubscribe = controlPoint.MSearchResponseObservable
    .Subscribe(
    res =>
    {
    	//Insert your code to handle M-SEARCH Reponses here
    }
    
var notifySubscribe = controlPoint.NotifyObservable
    .Subscribe(
    n =>
    {
    	// Insert your code to handle NOTIFY messages here
    }        
```
#### Broadcasting a M-SEARCH Multicast Request
In UPnP a ControlPoint can ask all the devices on the local network to report back about themselves by broadcasting a multicast M-SEARCH Request. Here is how that might look:
```csharp
var mSearchMessage = new MSearch
{
	SearchCastMethod = CastMethod.Multicast,
	CPFN = "myFriendlyNameOfThisControlPoint",
	HostIp = "239.255.255.250", 	// Must be this IP - i.e. this is a multicast 
	HostPort = 1900, 			   // Must be port 1900 as defined in th specification
	MX = TimeSpan.FromSeconds(1),
	ST = "upnp:rootdevice",
	UserAgent = new UserAgent
	{
		OperatingSystem = "Windows",
		OperatingSystemVersion = "10.0",
		ProductName = "SSDP.UPNP.PCL",
		ProductVersion = "0.9",
		UpnpMajorVersion = "2",
		UpnpMinorVersion = "0",
	}
};

await controlPoint.SendMSearch(mSearchMessage);
```
For details about what a multicast M-SEARCH Request is and how to use it: see the [UPnP Architecture documentation](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)). 
