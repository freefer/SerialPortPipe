using DataReader;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal class Program
    {

        static async Task Main(string[] args)
        {
            //var SerialService = new SerialService();
            //SerialService.Open(9600, 8, StopBits.One, Parity.None, "COM1");

            //Console.ReadLine();

      
            var spp = new SerialPortPipe();
            spp.Open(9600, 8, StopBits.One, Parity.None, "COM1");
            Console.ReadLine();
            spp.Close();
            Console.WriteLine("已关闭");
            Console.ReadLine();
            return;

        }

     
    }
}

