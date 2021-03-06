﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class DataFrameHeader
    {
        private bool _fin;
        private bool _rsv1;
        private bool _rsv2;
        private bool _rsv3;
        private sbyte _opcode;
        private bool _maskcode;
        private sbyte _payloadlength;

        public bool FIN { get { return _fin; } }

        public bool RSV1 { get { return _rsv1; } }

        public bool RSV2 { get { return _rsv2; } }

        public bool RSV3 { get { return _rsv3; } }

        public sbyte OpCode { get { return _opcode; } }

        public bool HasMask { get { return _maskcode; } }

        public sbyte Length { get { return _payloadlength; } }

        public DataFrameHeader(byte[] buffer)
        {
            if (buffer.Length < 2)
                throw new Exception("无效的数据头");

            //第一个字节
            _fin = (buffer[0] & 0x80) == 0x80;      //表示是否是最后一个帧，1代表是，0不是。返回数据帧给前端的时候FIN一定要为1，不然前端收不到
            _rsv1 = (buffer[0] & 0x40) == 0x40;     //RSV 1 RSV2 RSV3 留以后备用
            _rsv2 = (buffer[0] & 0x20) == 0x20;
            _rsv3 = (buffer[0] & 0x10) == 0x10;
            _opcode = (sbyte)(buffer[0] & 0x0f);    //帧类型，1代表文本数据，2代表二进制数据。这个影响前端onmessage接收的数据类型到底是String还是Blob

            //第二个字节
            _maskcode = (buffer[1] & 0x80) == 0x80; //1bit 掩码，是否加密数据，默认必须置为1
            _payloadlength = (sbyte)(buffer[1] & 0x7f); //7bit，表示数据的长度
        }

        //发送封装数据
        public DataFrameHeader(bool fin, bool rsv1, bool rsv2, bool rsv3, sbyte opcode, bool hasmask, int length)
        {
            _fin = fin;
            _rsv1 = rsv1;
            _rsv2 = rsv2;
            _rsv3 = rsv3;
            _opcode = opcode;
            //第二个字节
            _maskcode = hasmask;
            _payloadlength = (sbyte)length;
        }

        //返回帧头字节
        public byte[] GetBytes()
        {
            byte[] buffer = new byte[2] { 0, 0 };

            if (_fin) buffer[0] ^= 0x80;
            if (_rsv1) buffer[0] ^= 0x40;
            if (_rsv2) buffer[0] ^= 0x20;
            if (_rsv3) buffer[0] ^= 0x10;

            buffer[0] ^= (byte)_opcode;

            if (_maskcode) buffer[1] ^= 0x80;

            buffer[1] ^= (byte)_payloadlength;

            return buffer;
        }
    }
}

/*

    https://tools.ietf.org/html/rfc6455
      0                   1                   2                   3
      0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
     +-+-+-+-+-------+-+-------------+-------------------------------+
     |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
     |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
     |N|V|V|V|       |S|             |   (if payload len==126/127)   |
     | |1|2|3|       |K|             |                               |
     +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
     |     Extended payload length continued, if payload len == 127  |
     + - - - - - - - - - - - - - - - +-------------------------------+
     |                               |Masking-key, if MASK set to 1  |
     +-------------------------------+-------------------------------+
     | Masking-key (continued)       |          Payload Data         |
     +-------------------------------- - - - - - - - - - - - - - - - +
     :                     Payload Data continued ...                :
     + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
     |                     Payload Data continued ...                |
     +---------------------------------------------------------------+

*/
