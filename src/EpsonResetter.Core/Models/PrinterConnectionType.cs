namespace EpsonResetter.Core.Models;

public enum PrinterConnectionType
{
    Unknown = 0,
    Usb = 1,
    Network = 2,
    Local = 3
}

public enum PrinterCondition
{
    Unknown = 0,
    Normal = 1,
    Offline = 2,
    Error = 3
}