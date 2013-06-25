using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Tucson.MongoClient {
    /// <summary>Represents an ObjectId Generator.</summary>
    internal static class ObjectIdGenerator {
        
        #region private static fields
        /// <summary>The epoch.</summary>
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        /// <summary>The inclock.</summary>
        private static readonly object Inclock = new object();

        /// <summary>The inc.</summary>
        private static int _inc;

        /// <summary>The machine hash.</summary>
        private static readonly byte[] MachineHash = GenerateHostHash();

        /// <summary>The proc id.</summary>
        private static readonly byte[] ProcId = BitConverter.GetBytes(GenerateProcId());
        #endregion

        #region private static methods
        /// <summary>Generates time.</summary>
        /// <returns>The time.</returns>
        private static int GenerateTime() {
            var now = DateTime.Now.ToUniversalTime();

            var nowtime = new DateTime(Epoch.Year, Epoch.Month, Epoch.Day, now.Hour, now.Minute, now.Second, now.Millisecond);
            var diff = nowtime - Epoch;
            return Convert.ToInt32(Math.Floor(diff.TotalMilliseconds));
        }

        /// <summary>Generate an increment.</summary>
        /// <returns>The increment.</returns>
        private static int GenerateInc() {
            lock (Inclock) {
                return _inc++;
            }
        }

        /// <summary>Generates a host hash.</summary>
        private static byte[] GenerateHostHash() {
            using (var md5 = MD5.Create()) {
                var host = Dns.GetHostName();
                return md5.ComputeHash(Encoding.Default.GetBytes(host));
            }
        }

        /// <summary>Generates a proc id.</summary>
        /// <returns>Proc id.</returns>
        private static int GenerateProcId() {
            var proc = Process.GetCurrentProcess();
            return proc.Id;
        }
        #endregion

        #region public static methods
        /// <summary>Generates a byte array ObjectId.</summary>
        public static byte[] Generate() {
            var oid = new byte[12];
            var copyidx = 0;

            Array.Copy(BitConverter.GetBytes(GenerateTime()), 0, oid, copyidx, 4);
            copyidx += 4;

            Array.Copy(MachineHash, 0, oid, copyidx, 3);
            copyidx += 3;

            Array.Copy(ProcId, 0, oid, copyidx, 2);
            copyidx += 2;

            Array.Copy(BitConverter.GetBytes(GenerateInc()), 0, oid, copyidx, 3);
            return oid;
        }
        #endregion
    }
}