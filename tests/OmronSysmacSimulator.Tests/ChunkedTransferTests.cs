using System;
using Xunit;
using OmronSysmacSimulator.Transfer;

namespace OmronSysmacSimulator.Tests
{
    public class ChunkedTransferTests
    {
        [Fact]
        public void DefaultChunkSize_Is512()
        {
            var transfer = new ChunkedTransfer();
            Assert.Equal(512, transfer.MaxChunkSize);
        }

        [Fact]
        public void SetFixedChunkSize_UpdatesMaxChunkSize()
        {
            var transfer = new ChunkedTransfer();
            transfer.SetFixedChunkSize(1024);
            Assert.Equal(1024, transfer.MaxChunkSize);
            Assert.False(transfer.WasAutoDetected);
        }

        [Fact]
        public void SetFixedChunkSize_BelowMinimum_Throws()
        {
            var transfer = new ChunkedTransfer();
            Assert.Throws<ArgumentOutOfRangeException>(() => transfer.SetFixedChunkSize(100));
        }

        [Fact]
        public void CalculateChunkCount_SingleChunk_ReturnsOne()
        {
            var transfer = new ChunkedTransfer();
            transfer.SetFixedChunkSize(512);
            Assert.Equal(1, transfer.CalculateChunkCount(512));
            Assert.Equal(1, transfer.CalculateChunkCount(100));
            Assert.Equal(1, transfer.CalculateChunkCount(1));
        }

        [Fact]
        public void CalculateChunkCount_MultipleChunks_ReturnsCorrectCount()
        {
            var transfer = new ChunkedTransfer();
            transfer.SetFixedChunkSize(512);
            Assert.Equal(2, transfer.CalculateChunkCount(513));
            Assert.Equal(2, transfer.CalculateChunkCount(1024));
            Assert.Equal(3, transfer.CalculateChunkCount(1025));
            Assert.Equal(4, transfer.CalculateChunkCount(2000));
        }

        [Fact]
        public void CalculateOffsetAddress_ZeroOffset_ReturnsSameAddress()
        {
            var result = ChunkedTransfer.CalculateOffsetAddress("1,0,0,0,80", 0);
            Assert.Equal("1,0,0,0,80", result);
        }

        [Fact]
        public void CalculateOffsetAddress_NonZeroOffset_UpdatesFourthComponent()
        {
            var result = ChunkedTransfer.CalculateOffsetAddress("1,0,0,0,80", 10);
            Assert.Equal("1,0,0,10,80", result);
        }

        [Fact]
        public void CalculateOffsetAddress_ExistingOffset_AddsToIt()
        {
            var result = ChunkedTransfer.CalculateOffsetAddress("1,0,0,100,80", 50);
            Assert.Equal("1,0,0,150,80", result);
        }

        [Fact]
        public void ReadChunked_SmallData_SingleRead()
        {
            var transfer = new ChunkedTransfer();
            transfer.SetFixedChunkSize(512);
            
            int readCount = 0;
            byte[] expectedData = new byte[] { 1, 2, 3, 4, 5 };

            var result = transfer.ReadChunked("1,0,0,0,80", 5, (addr, size) =>
            {
                readCount++;
                Assert.Equal("1,0,0,0,80", addr);
                Assert.Equal(5, size);
                return expectedData;
            });

            Assert.Equal(1, readCount);
            Assert.Equal(expectedData, result);
        }

        [Fact]
        public void ReadChunked_LargeData_MultipleReads()
        {
            var transfer = new ChunkedTransfer();
            transfer.SetFixedChunkSize(256);

            int readCount = 0;
            var addressesRead = new System.Collections.Generic.List<string>();

            var result = transfer.ReadChunked("1,0,0,0,80", 600, (addr, size) =>
            {
                readCount++;
                addressesRead.Add(addr);
                var chunk = new byte[size];
                for (int i = 0; i < size; i++)
                {
                    chunk[i] = (byte)(readCount * 10 + i % 10);
                }
                return chunk;
            });

            Assert.Equal(3, readCount); // 256 + 256 + 88 = 600
            Assert.Equal(600, result.Length);
            Assert.Equal("1,0,0,0,80", addressesRead[0]);
            Assert.Equal("1,0,0,256,80", addressesRead[1]);
            Assert.Equal("1,0,0,512,80", addressesRead[2]);
        }

        [Fact]
        public void WriteChunked_SmallData_SingleWrite()
        {
            var transfer = new ChunkedTransfer();
            transfer.SetFixedChunkSize(512);

            int writeCount = 0;
            byte[] dataToWrite = new byte[] { 1, 2, 3, 4, 5 };

            transfer.WriteChunked("1,0,0,0,80", dataToWrite, (addr, data) =>
            {
                writeCount++;
                Assert.Equal("1,0,0,0,80", addr);
                Assert.Equal(dataToWrite, data);
            });

            Assert.Equal(1, writeCount);
        }

        [Fact]
        public void WriteChunked_LargeData_MultipleWrites()
        {
            var transfer = new ChunkedTransfer();
            transfer.SetFixedChunkSize(256);

            int writeCount = 0;
            var addressesWritten = new System.Collections.Generic.List<string>();
            var sizesWritten = new System.Collections.Generic.List<int>();
            
            byte[] dataToWrite = new byte[600];
            for (int i = 0; i < dataToWrite.Length; i++)
            {
                dataToWrite[i] = (byte)i;
            }

            transfer.WriteChunked("1,0,0,0,80", dataToWrite, (addr, data) =>
            {
                writeCount++;
                addressesWritten.Add(addr);
                sizesWritten.Add(data.Length);
            });

            Assert.Equal(3, writeCount);
            Assert.Equal("1,0,0,0,80", addressesWritten[0]);
            Assert.Equal("1,0,0,256,80", addressesWritten[1]);
            Assert.Equal("1,0,0,512,80", addressesWritten[2]);
            Assert.Equal(256, sizesWritten[0]);
            Assert.Equal(256, sizesWritten[1]);
            Assert.Equal(88, sizesWritten[2]);
        }

        [Fact]
        public void DetectMaxChunkSize_AllSucceed_UsesLargest()
        {
            var transfer = new ChunkedTransfer();

            transfer.DetectMaxChunkSize(size => true);

            Assert.Equal(4096, transfer.MaxChunkSize);
            Assert.True(transfer.WasAutoDetected);
        }

        [Fact]
        public void DetectMaxChunkSize_FailsAt1024_Uses512()
        {
            var transfer = new ChunkedTransfer();

            transfer.DetectMaxChunkSize(size => size < 1024);

            Assert.Equal(512, transfer.MaxChunkSize);
            Assert.True(transfer.WasAutoDetected);
        }

        [Fact]
        public void DetectMaxChunkSize_AllFail_UsesMinimum()
        {
            var transfer = new ChunkedTransfer();

            transfer.DetectMaxChunkSize(size => false);

            Assert.Equal(256, transfer.MaxChunkSize);
            Assert.True(transfer.WasAutoDetected);
        }

        [Fact]
        public void DetectMaxChunkSize_ExceptionThrown_StopsAndUsesLastGood()
        {
            var transfer = new ChunkedTransfer();
            int callCount = 0;

            transfer.DetectMaxChunkSize(size =>
            {
                callCount++;
                if (size >= 1024)
                    throw new Exception("Simulated failure");
                return true;
            });

            Assert.Equal(512, transfer.MaxChunkSize);
            Assert.True(transfer.WasAutoDetected);
        }
    }
}
