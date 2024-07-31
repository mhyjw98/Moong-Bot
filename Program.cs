// See https://aka.ms/new-console-template for more information
using MoongBot.Core;
using System;

namespace MoongBot
{
    class Program
    {
        static void Main(string[] args)
            => new Bot().MainAsync().GetAwaiter().GetResult();
    }
}

