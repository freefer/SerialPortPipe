using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class SerialPortPipe
    {

        private readonly SerialPort serialPort;

        private readonly Pipe pipe;

        private PipeReader reader;
        private PipeWriter writer;

        private Task writing;
        private Task reading;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private byte[] _beginMark = new byte[] { 0xBB, 0x55 };
        private byte[] _endMark = new byte[] { 0X7E, 0X7E };

        public int MaxPackageLength { get; set; } = 1024 * 1024;
        public SerialPortPipe()
        {
            serialPort = new SerialPort();
            pipe = new Pipe();
            reader = pipe.Reader;
            writer = pipe.Writer;
        }

        public void Open(int baudRate, int dataBits, StopBits stopBits, Parity parity, string name)
        {
            serialPort.BaudRate = baudRate;
            serialPort.DataBits = dataBits;
            serialPort.Parity = parity;
            serialPort.StopBits = stopBits;
            serialPort.PortName = name;
            serialPort.Open();
            _ = ReadDataAsync();
        }
        private async Task ReadDataAsync()
        {
            writing = FillPipeAsync();
            reading = ReadPipeAsync();

            await Task.WhenAll(writing, reading).ConfigureAwait(false);

        }

        public async Task FillPipeAsync()
        {


            while (!_cts.IsCancellationRequested && serialPort.IsOpen)
            {
                try
                {
                    byte[] buffer = new byte[serialPort.ReadBufferSize];
                    int bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await writer.WriteAsync(buffer.AsMemory(0, bytesRead));

                    //Make the data available to the PipeReader.
                    FlushResult result = await writer.FlushAsync();

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {

                    throw;
                }
            }

            await writer.CompleteAsync();


        }

        /// <summary>
        /// 读取Pip管道数据
        /// </summary>
        /// <returns></returns>
        public async Task ReadPipeAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                ReadResult result = default;

                try
                {
                    result = await reader.ReadAsync(_cts.Token).ConfigureAwait(false);

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                var buffer = result.Buffer;

                if (result.IsCanceled)
                {
                    break;
                }

                var completed = result.IsCompleted;

                try
                {
                    while (Filter(ref buffer, out ReadOnlySequence<byte> package))
                    {
                        Console.WriteLine(BitConverter.ToString(package.ToArray()));
                    }

                    if (completed)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);

                }
                finally
                {
                    reader.AdvanceTo(buffer.Start, buffer.End);

                }
            }

            await reader.CompleteAsync();
        }



        public bool Filter(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> package)
        {
            package = default;
            bool foundHeader = false;
            bool foundFooter = false;
            int headerIndex = 0;
            int packetLength = 0;
            // 缓存 start.Length 和 end.Length
            int startLength = _beginMark.Length;
            int endLength = _endMark.Length;

            // 将 Span 提到循环外部来避免多次内存拷贝，
            // 使用 ref 来减少将 ReadOnlyMemory<byte> 转换为 ReadOnlySpan<byte> 的开销
            ReadOnlySpan<byte> span = default;

            foreach (var segment in buffer)
            {
                span = segment.Span;

                if (!foundHeader)
                {
                    headerIndex = span.IndexOf(_beginMark);
                    if (headerIndex >= 0)
                    {
                        foundHeader = true;
                        span = span.Slice(headerIndex + startLength);
                        packetLength += startLength;  // 更新包长
                    }
                }

                if (foundHeader && !foundFooter)
                {
                    int footerIndex = span.IndexOf(_endMark);
                    if (footerIndex >= 0)
                    {
                        foundFooter = true;
                        packetLength += footerIndex + endLength; // 更新包长
                    }
                    else
                    {
                        packetLength += span.Length; // 更新包长
                    }
                }
            }
            //没有找到包头并且数据长度大于包头 视为无效数据
            if (!foundHeader && buffer.Length >= startLength)
            {
                buffer = buffer.Slice(buffer.End);
            }
            // 匹配到包头和包尾
            if (foundHeader && foundFooter)
            {
                package = buffer.Slice(headerIndex, packetLength);

                // 切割掉已处理的数据
                buffer = buffer.Slice(headerIndex + packetLength);
                return true;
            }
            if (buffer.Length >= MaxPackageLength)
            {
                buffer = buffer.Slice(buffer.End);
                throw new Exception("超出包最大长度");
            }
            return false;
        }
        public async void Close()
        {
            _cts.Cancel();
            await Task.WhenAll(writing).ConfigureAwait(false);
            serialPort.Close();

        }


        public void Send(byte[] buffer, int offset, int count)
        {
            serialPort.Write(buffer, offset, count);
        }

    }
}
