using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusTcpFull
{
    /// <summary>
    /// ModbusTCP主站（客户端）- 支持所有核心功能码
    /// </summary>
    public class ModbusTcpMaster : IDisposable
    {
        private readonly TcpClient _tcpClient;
#nullable enable
        private NetworkStream? _networkStream;
#nullable disable
        private readonly string _ipAddress;
        private readonly int _port;
        private byte slaveId;

        public ModbusTcpMaster(string ipAddress, int port = ModbusTcpConstants.DefaultPort, byte slaveId = ModbusTcpConstants.DefaultUnitId)
        {
            _ipAddress = ipAddress;
            _port = port;
            _tcpClient = new TcpClient { NoDelay = true }; // 禁用Nagle算法，降低延迟
            this.slaveId = slaveId;
        }

        /// <summary>
        /// 连接从站
        /// </summary>
        public async Task<bool> ConnectAsync(CancellationToken token = default)
        {
            try
            {
                if (_tcpClient.Connected) return true;
                await _tcpClient.ConnectAsync(IPAddress.Parse(_ipAddress), _port, token);
                _networkStream = _tcpClient.GetStream();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连接失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _networkStream?.Close();
            _tcpClient?.Close();
        }

        #region 核心功能实现
        /// <summary>
        /// 01 - 读线圈状态
        /// </summary>
        /// <param name="startAddress">起始地址（0-65535）</param>
        /// <param name="quantity">读取数量（1-2000）</param>
        /// <returns>线圈状态数组（true=ON，false=OFF）</returns>
        public async Task<bool[]> ReadCoilsAsync(ushort transId, ushort startAddress, ushort quantity, CancellationToken token = default)
        {
            ValidateReadParameters(quantity, ModbusTcpConstants.MaxCoilsPerRequest);
            var request = BuildReadRequest(transId, ModbusFunctionCode.ReadCoils, startAddress, quantity);
            var response = await SendAndReceiveAsync(request, token);
            // 检验transId一致性
            var respTransId = (ushort)((response[0] << 8) | response[1]);
            if (respTransId != transId)
                throw new InvalidOperationException("响应的事务ID与请求不匹配");
            return ParseCoilResponse(response, quantity);
        }

        /// <summary>
        /// 02 - 读离散输入
        /// </summary>
        public async Task<bool[]> ReadDiscreteInputsAsync(ushort transId, ushort startAddress, ushort quantity, CancellationToken token = default)
        {
            ValidateReadParameters(quantity, ModbusTcpConstants.MaxCoilsPerRequest);
            var request = BuildReadRequest(transId, ModbusFunctionCode.ReadDiscreteInputs, startAddress, quantity);
            var response = await SendAndReceiveAsync(request, token);
            // 检验transId一致性
            var respTransId = (ushort)((response[0] << 8) | response[1]);
            if (respTransId != transId)
                throw new InvalidOperationException("响应的事务ID与请求不匹配");
            return ParseCoilResponse(response, quantity);
        }

        /// <summary>
        /// 03 - 读保持寄存器
        /// </summary>
        public async Task<ushort[]> ReadHoldingRegistersAsync(ushort transId, ushort startAddress, ushort quantity, CancellationToken token = default)
        {
            ValidateReadParameters(quantity, ModbusTcpConstants.MaxRegistersPerRequest);
            var request = BuildReadRequest(transId, ModbusFunctionCode.ReadHoldingRegisters, startAddress, quantity);
            var response = await SendAndReceiveAsync(request, token);
            // 检验transId一致性
            var respTransId = (ushort)((response[0] << 8) | response[1]);
            if (respTransId != transId)
                throw new InvalidOperationException("响应的事务ID与请求不匹配");
            return ParseRegisterResponse(response, quantity);
        }

        /// <summary>
        /// 04 - 读输入寄存器
        /// </summary>
        public async Task<ushort[]> ReadInputRegistersAsync(ushort transId, ushort startAddress, ushort quantity, CancellationToken token = default)
        {
            ValidateReadParameters(quantity, ModbusTcpConstants.MaxRegistersPerRequest);
            var request = BuildReadRequest(transId, ModbusFunctionCode.ReadInputRegisters, startAddress, quantity);
            var response = await SendAndReceiveAsync(request, token);
            // 检验transId一致性
            var respTransId = (ushort)((response[0] << 8) | response[1]);
            if (respTransId != transId)
                throw new InvalidOperationException("响应的事务ID与请求不匹配");
            return ParseRegisterResponse(response, quantity);
        }

        /// <summary>
        /// 05 - 写单个线圈
        /// </summary>
        /// <param name="address">线圈地址</param>
        /// <param name="value">true=ON，false=OFF</param>
        public async Task<bool> WriteSingleCoilAsync(ushort transId, ushort address, bool value, CancellationToken token = default)
        {
            var request = BuildWriteSingleCoilRequest(transId, address, value);
            var response = await SendAndReceiveAsync(request, token);
            return ParseWriteSingleCoilResponse(response, address, value);
        }

        /// <summary>
        /// 06 - 写单个保持寄存器
        /// </summary>
        public async Task<bool> WriteSingleRegisterAsync(ushort transId, ushort address, ushort value, CancellationToken token = default)
        {
            var request = BuildWriteSingleRegisterRequest(transId, address, value);
            var response = await SendAndReceiveAsync(request, token);
            return ParseWriteSingleRegisterResponse(response, address, value);
        }

        /// <summary>
        /// 15 - 写多个线圈
        /// </summary>
        public async Task<bool> WriteMultipleCoilsAsync(ushort transId, ushort startAddress, bool[] values, CancellationToken token = default)
        {
            if (values == null || values.Length == 0 || values.Length > ModbusTcpConstants.MaxCoilsPerRequest)
                throw new ArgumentOutOfRangeException(nameof(values), "线圈数量必须1-2000");

            var request = BuildWriteMultipleCoilsRequest(transId, startAddress, values);
            var response = await SendAndReceiveAsync(request, token);
            return ParseWriteMultipleCoilsResponse(response, startAddress, (ushort)values.Length);
        }

        /// <summary>
        /// 16 - 写多个保持寄存器
        /// </summary>
        public async Task<bool> WriteMultipleRegistersAsync(ushort transId, ushort startAddress, ushort[] values, CancellationToken token = default)
        {
            if (values == null || values.Length == 0 || values.Length > ModbusTcpConstants.MaxRegistersPerRequest)
                throw new ArgumentOutOfRangeException(nameof(values), "寄存器数量必须1-125");

            var request = BuildWriteMultipleRegistersRequest(transId, startAddress, values);
            var response = await SendAndReceiveAsync(request, token);
            return ParseWriteMultipleRegistersResponse(response, startAddress, (ushort)values.Length);
        }
        #endregion

        #region 报文构建与解析
        // 构建读请求报文（01/02/03/04）
        private byte[] BuildReadRequest(ushort transId, ModbusFunctionCode funcCode, ushort startAddress, ushort quantity)
        {
            byte[] request = new byte[12];
            // MBAP头
            WriteTransactionId(request, transId);
            WriteProtocolId(request);
            request[4] = 0x00; request[5] = 0x06; // 长度（单元ID+功能码+地址+数量=6）
            request[6] = slaveId; // 单元ID
            // 应用数据
            request[7] = (byte)funcCode; // 功能码
            request[8] = (byte)(startAddress >> 8); // 起始地址高字节
            request[9] = (byte)(startAddress & 0xFF); // 起始地址低字节
            request[10] = (byte)(quantity >> 8); // 数量高字节
            request[11] = (byte)(quantity & 0xFF); // 数量低字节
            return request;
        }

        // 构建写单个线圈请求（05）
        private byte[] BuildWriteSingleCoilRequest(ushort transId, ushort address, bool value)
        {
            byte[] request = new byte[12];
            WriteTransactionId(request, transId);
            WriteProtocolId(request);
            request[4] = 0x00; request[5] = 0x06;
            request[6] = slaveId;
            request[7] = (byte)ModbusFunctionCode.WriteSingleCoil;
            request[8] = (byte)(address >> 8);
            request[9] = (byte)(address & 0xFF);
            ushort coilValue = value ? ModbusTcpConstants.CoilOnValue : ModbusTcpConstants.CoilOffValue;
            request[10] = (byte)(coilValue >> 8);
            request[11] = (byte)(coilValue & 0xFF);
            return request;
        }

        // 构建写单个寄存器请求（06）
        private byte[] BuildWriteSingleRegisterRequest(ushort transId, ushort address, ushort value)
        {
            byte[] request = new byte[12];
            WriteTransactionId(request, transId);
            WriteProtocolId(request);
            request[4] = 0x00; request[5] = 0x06;
            request[6] = slaveId;
            request[7] = (byte)ModbusFunctionCode.WriteSingleRegister;
            request[8] = (byte)(address >> 8);
            request[9] = (byte)(address & 0xFF);
            request[10] = (byte)(value >> 8);
            request[11] = (byte)(value & 0xFF);
            return request;
        }

        // 构建写多个线圈请求（15）
        private byte[] BuildWriteMultipleCoilsRequest(ushort transId, ushort startAddress, bool[] values)
        {
            int byteCount = (values.Length + 7) / 8; // 计算字节数（向上取整）
            byte[] request = new byte[13 + byteCount];
            // MBAP头
            WriteTransactionId(request, transId);
            WriteProtocolId(request);
            request[4] = (byte)((7 + byteCount) >> 8); // 长度=单元ID(1)+功能码(1)+地址(2)+数量(2)+字节数(1)+数据(n)
            request[5] = (byte)((7 + byteCount) & 0xFF);
            request[6] = slaveId;
            // 应用数据
            request[7] = (byte)ModbusFunctionCode.WriteMultipleCoils;
            request[8] = (byte)(startAddress >> 8);
            request[9] = (byte)(startAddress & 0xFF);
            request[10] = (byte)((ushort)values.Length >> 8);
            request[11] = (byte)((ushort)values.Length & 0xFF);
            request[12] = (byte)byteCount;
            // 线圈数据（按位填充）
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i])
                    request[13 + (i / 8)] |= (byte)(1 << (i % 8));
            }
            return request;
        }

        // 构建写多个寄存器请求（16）
        private byte[] BuildWriteMultipleRegistersRequest(ushort transId, ushort startAddress, ushort[] values)
        {
            int byteCount = values.Length * 2;
            byte[] request = new byte[13 + byteCount];
            // MBAP头
            WriteTransactionId(request, transId);
            WriteProtocolId(request);
            request[4] = (byte)((7 + byteCount) >> 8);
            request[5] = (byte)((7 + byteCount) & 0xFF);
            request[6] = slaveId;
            // 应用数据
            request[7] = (byte)ModbusFunctionCode.WriteMultipleRegisters;
            request[8] = (byte)(startAddress >> 8);
            request[9] = (byte)(startAddress & 0xFF);
            request[10] = (byte)((ushort)values.Length >> 8);
            request[11] = (byte)((ushort)values.Length & 0xFF);
            request[12] = (byte)byteCount;
            // 寄存器数据
            for (int i = 0; i < values.Length; i++)
            {
                request[13 + i * 2] = (byte)(values[i] >> 8);
                request[14 + i * 2] = (byte)(values[i] & 0xFF);
            }
            return request;
        }

        // 解析线圈响应（01/02）
        private bool[] ParseCoilResponse(byte[] response, ushort quantity)
        {
            CheckException(response);
            int byteCount = response[8];
            bool[] coils = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                int byteIndex = 9 + (i / 8);
                int bitIndex = i % 8;
                coils[i] = (response[byteIndex] & (1 << bitIndex)) != 0;
            }
            return coils;
        }

        // 解析寄存器响应（03/04）
        private ushort[] ParseRegisterResponse(byte[] response, ushort quantity)
        {
            CheckException(response);
            int byteCount = response[8];
            ushort[] registers = new ushort[quantity];
            for (int i = 0; i < quantity; i++)
            {
                registers[i] = (ushort)((response[9 + i * 2] << 8) | response[10 + i * 2]);
            }
            return registers;
        }

        // 解析写单个线圈响应（05）
        private bool ParseWriteSingleCoilResponse(byte[] response, ushort address, bool value)
        {
            CheckException(response);
            ushort respAddress = (ushort)((response[8] << 8) | response[9]);
            ushort respValue = (ushort)((response[10] << 8) | response[11]);
            bool respOn = respValue == ModbusTcpConstants.CoilOnValue;
            return respAddress == address && respOn == value;
        }

        // 解析写单个寄存器响应（06）
        private bool ParseWriteSingleRegisterResponse(byte[] response, ushort address, ushort value)
        {
            CheckException(response);
            ushort respAddress = (ushort)((response[8] << 8) | response[9]);
            ushort respValue = (ushort)((response[10] << 8) | response[11]);
            return respAddress == address && respValue == value;
        }

        // 解析写多个线圈响应（15）
        private bool ParseWriteMultipleCoilsResponse(byte[] response, ushort startAddress, ushort quantity)
        {
            CheckException(response);
            ushort respAddress = (ushort)((response[8] << 8) | response[9]);
            ushort respQuantity = (ushort)((response[10] << 8) | response[11]);
            return respAddress == startAddress && respQuantity == quantity;
        }

        // 解析写多个寄存器响应（16）
        private bool ParseWriteMultipleRegistersResponse(byte[] response, ushort startAddress, ushort quantity)
        {
            CheckException(response);
            ushort respAddress = (ushort)((response[8] << 8) | response[9]);
            ushort respQuantity = (ushort)((response[10] << 8) | response[11]);
            return respAddress == startAddress && respQuantity == quantity;
        }
        #endregion

        #region 辅助方法
        // 发送并接收报文
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private async Task<byte[]> SendAndReceiveAsync(byte[] request, CancellationToken token)
        {
            if (!_tcpClient.Connected || _networkStream == null)
                throw new InvalidOperationException("未连接到ModbusTCP从站");

            await _semaphore.WaitAsync(token);
            try
            {
                // 发送请求
                await _networkStream.WriteAsync(request, token);
                await _networkStream.FlushAsync(token);

                // 接收响应（先读MBAP头，再读数据）
                byte[] header = new byte[7];
                int headerBytes = await _networkStream.ReadAsync(header, 0, 7, token);
                if (headerBytes != 7)
                    throw new InvalidOperationException("响应头不完整");

                // 解析长度（MBAP头第5-6字节）
                int dataLength = (header[4] << 8) | header[5];
                byte[] data = new byte[dataLength];
                int dataBytes = await _networkStream.ReadAsync(data, token) + 1;
                if (dataBytes != dataLength)
                    throw new InvalidOperationException("响应数据不完整");

                // 合并响应（MBAP头+数据）
                byte[] response = new byte[7 + dataLength];
                Array.Copy(header, 0, response, 0, 7);
                Array.Copy(data, 0, response, 7, dataLength);

                return response;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // 写入事务ID（自增）
        private void WriteTransactionId(byte[] buffer, ushort transId)
        {
            buffer[0] = (byte)(transId >> 8);
            buffer[1] = (byte)(transId & 0xFF);
        }

        // 写入协议ID（固定0）
        private void WriteProtocolId(byte[] buffer)
        {
            buffer[2] = (byte)(ModbusTcpConstants.ProtocolId >> 8);
            buffer[3] = (byte)(ModbusTcpConstants.ProtocolId & 0xFF);
        }

        // 校验读参数
        private void ValidateReadParameters(ushort quantity, ushort max)
        {
            if (quantity < 1 || quantity > max)
                throw new ArgumentOutOfRangeException(nameof(quantity), $"数量必须1-{max}");
        }

        // 检查异常响应
        private void CheckException(byte[] response)
        {
            byte funcCode = response[7];
            if ((funcCode & 0x80) != 0) // 功能码最高位为1表示异常
            {
                ModbusExceptionCode exCode = (ModbusExceptionCode)response[8];
                throw new InvalidOperationException($"Modbus异常：功能码={(ModbusFunctionCode)(funcCode & 0x7F)}，异常码={exCode}");
            }
        }
        #endregion

        public void Dispose()
        {
            Disconnect();
            _tcpClient.Dispose();
        }
    }
}