using System;

namespace ModbusTcpFull
{
    /// <summary>
    /// Modbus功能码枚举
    /// </summary>
    public enum ModbusFunctionCode : byte
    {
        ReadCoils = 0x01,               // 读线圈
        ReadDiscreteInputs = 0x02,      // 读离散输入
        ReadHoldingRegisters = 0x03,    // 读保持寄存器
        ReadInputRegisters = 0x04,      // 读输入寄存器
        WriteSingleCoil = 0x05,         // 写单个线圈
        WriteSingleRegister = 0x06,     // 写单个寄存器
        WriteMultipleCoils = 0x0F,      // 写多个线圈（15）
        WriteMultipleRegisters = 0x10,  // 写多个寄存器（16）
        ExceptionBase = 0x80            // 异常功能码偏移（功能码+0x80为异常）
    }

    /// <summary>
    /// Modbus异常码枚举
    /// </summary>
    public enum ModbusExceptionCode : byte
    {
        IllegalFunction = 0x01,         // 非法功能码
        IllegalDataAddress = 0x02,      // 非法数据地址
        IllegalDataValue = 0x03,        // 非法数据值
        SlaveDeviceFailure = 0x04,      // 从站设备故障
        Acknowledge = 0x05,             // 确认
        SlaveDeviceBusy = 0x06,         // 从站忙
        MemoryParityError = 0x08,       // 内存奇偶校验错误
        GatewayPathUnavailable = 0x0A,  // 网关路径不可用
        GatewayTargetDeviceFailed = 0x0B// 网关目标设备失败
    }
        
    /// <summary>
    /// ModbusTCP常量
    /// </summary>
    public static class ModbusTcpConstants
    {
        public const int DefaultPort = 502;          // 默认端口
        public const ushort ProtocolId = 0x0000;     // 协议ID（固定为0）
        public const byte DefaultUnitId = 0x01;      // 默认单元ID
        public const int MaxCoilsPerRequest = 2000;  // 单次读线圈最大数量
        public const int MaxRegistersPerRequest = 125;// 单次读寄存器最大数量
        public const ushort CoilOnValue = 0xFF00;    // 线圈置1的值
        public const ushort CoilOffValue = 0x0000;   // 线圈置0的值
    }
}