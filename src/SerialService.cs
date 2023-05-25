using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataReader
{
    public class SerialService
    {

        private readonly SerialPort serialPort;

        public SerialService()
        {
            serialPort = new SerialPort();
            serialPort.DataReceived += _serialPort_DataReceived;

            _readBuffer = new List<byte>();

        }

        public event EventHandler<MessagePacketData> MessageDataReceived;
        protected virtual void OnMessageDataReceived(MessagePacketData e)
        {
            var handler = MessageDataReceived;
            if (handler != null) handler(this, e);
        }
        public void Open(int baudRate, int dataBits, StopBits stopBits, Parity parity, string name)
        {

            serialPort.BaudRate = baudRate;
            serialPort.DataBits = dataBits;
            serialPort.Parity = parity;
            serialPort.StopBits = stopBits;
            serialPort.PortName = name;
            serialPort.Open();
        }

        public void Close()
        {

            serialPort.Close();
        }


        private List<byte> _readBuffer;


        

        private int bufferLength;


        private int parsedLengthInBuffer;

        private int offset;

        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            var bytes = new byte[serialPort.BytesToRead];
            var read = serialPort.Read(bytes, 0, serialPort.BytesToRead);
            bufferLength += read;
            _readBuffer.AddRange(bytes);

            ProcessRequest();
        }
        public string ToHexStrFromByte(byte[] byteDatas)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < byteDatas.Length; i++)
            {
                builder.Append(string.Format("{0:X2} ", byteDatas[i]));
            }
            return builder.ToString().Trim();

        }
        public void ProcessRequest()
        {
            var mark = new byte[] { 0xBB, 0x55 };
            var end = new byte[] { 0X7E, 0X7E };

             while (_readBuffer.Count > 0)
            {
                if (_readBuffer.Count > 3 && _readBuffer[0] == mark[0] && _readBuffer[1] == mark[1])
                {
                    var endIndex = FindMatchingBytes(_readBuffer, end, 0);
                    if (endIndex == -1)
                    {
                        return;
                    }

                    /*
                     解析包数据
                     注意内容需要转义
                     */
                    var bodyLenth = endIndex;
                    OnMessageDataReceived(new MessagePacketData()
                    {
                        Type = _readBuffer[2].ToString("00"),
                        Body = _readBuffer.Skip(2).Take(bodyLenth).ToArray()
                    });
                    var data = _readBuffer.Take(bodyLenth).ToArray();
                    Debug.WriteLine(ToHexStrFromByte(data));
                    _readBuffer.RemoveRange(0, endIndex);
                }
                else
                {

                    _readBuffer.RemoveRange(0, 1);
                }

            }


        }



        public int FindMatchingBytes(List<byte> buffer, byte[] startArray, int offset)
        {
            int startIndex = -1;


            for (int i = offset; i < buffer.Count; i++)
            {
                if (buffer[i] == startArray[0])
                {
                    bool match = true;
                    for (int j = 1; j < startArray.Length; j++)
                    {
                        if (i + j >= buffer.Count || buffer[i + j] != startArray[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        startIndex = i;


                        break;
                    }
                }
            }

            return startIndex;
        }

    }

    public class MessagePacketData : EventArgs
    {

        public MessagePacketData() { }
        public string Type { get; set; }
        public string Message { get; set; }
        public object[] Params { get; set; }

        public byte[] Body { get; set; }

    }

}
