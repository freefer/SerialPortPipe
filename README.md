# SerialPortPipe
serialport use System.IO.Pipelines parsing data
_beginMark = [0xBB,0x55]  _endMark=[0X7E, 0X7E]
data=_beginMark+...body...+_endMark
use Pipe read serialport base stream data and parsing package
