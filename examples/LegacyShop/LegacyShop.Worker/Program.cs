using System;
using System.Security.Cryptography;
using System.Threading;

var workerThread = new Thread(() => Thread.Sleep(Timeout.Infinite));
workerThread.Start();
workerThread.Abort();

var sandbox = AppDomain.CreateDomain("plugins");
using var rng = new RNGCryptoServiceProvider();
Console.WriteLine(sandbox.FriendlyName);
