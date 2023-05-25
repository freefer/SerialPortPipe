# SerialPortPipe
serialport use System.IO.Pipelines parsing data\n
_beginMark = [0xBB,0x55]  _endMark=[0X7E, 0X7E]\n
data=_beginMark+...body...+_endMark\n
use Pipe read serialport base stream data and parsing package
