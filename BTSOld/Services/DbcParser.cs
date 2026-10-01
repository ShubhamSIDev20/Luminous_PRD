using System.Text;
using System.Text.RegularExpressions;

namespace BatteryTestingSystem.Services
{

    #region Base Models

    public enum ByteOrder
    {
        Motorola = 0,   // Big Endian
        Intel = 1    // Little Endian
    }

    public enum DbcValueType
    {
        Unsigned = 0,   // + in DBC
        Signed = 1    // - in DBC
    }

    public enum CanBaudrate : byte
    {
        BR_250K,   // 250 kbps
        BR_500K,   // 500 kbps
        BR_750K,   // 800 kbps
        BR_1M    // 1 Mbps
    }

    public enum CanPort
    {
        Port1 = 1,
        Port2 = 2,
        Port3 = 3
    }
    /// <summary>
    /// Signal definition parsed from DBC file
    /// </summary>
    public class DbcSignal
    {
        public int SingalId { get; set; }
        public string Name { get; set; }
        public int StartBit { get; set; }
        public int BitLength { get; set; }
        public ByteOrder ByteOrder { get; set; }
        public DbcValueType ValueType { get; set; }
        public double Factor { get; set; }
        public double Offset { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public string Unit { get; set; }
        public string Comment { get; set; }
        public bool IsSelected { get; set; } = false;

        public DbcSignal()
        {
            Name = string.Empty;
            Unit = string.Empty;
            Comment = string.Empty;
        }


        /// <summary>
        /// Serialize signal to bytes
        /// Layout: SignalId(u8) | StartBit(u8) | BitLength(u8) | ValueType(u8) | ByteOrder(u8)
        /// </summary>
        public byte[] ToBytes()
        {
            // Helper methods to write numbers in big-endian
            void Writefloat(BinaryWriter writer, float value)
            {
                var bytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                writer.Write(bytes);
            }

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)StartBit);                  // StartBit   (uint8_t) — LSB position
                writer.Write((byte)BitLength);                 // BitLength  (uint8_t)
                writer.Write((byte)ValueType);                 // ValueType  (uint8_t) — 0:Unsigned, 1:Signed
                writer.Write((byte)ByteOrder);                 // Format     (uint8_t) — 0:Motorola(BE), 1:Intel(LE)
                writer.Write((byte)SingalId);                  // SignalId    (uint8_t) — 0 if not selected, else ++ assigned id
                Writefloat(writer, (float)Factor);             //Factor 
                Writefloat(writer, (float)Offset);             //Offset
                return ms.ToArray();
            }
        }

        public string ToHexValue(bool tx = false)
        {
            return string.Join(" ", ToBytes().Select(b => $"0x{b:X2}")) + Environment.NewLine;
        }
    }

    /// <summary>
    /// CAN Message definition parsed from DBC file
    /// </summary>
    public class DbcMessage
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public int DLC { get; set; } // Data Length Code
        public string Transmitter { get; set; }
        public string Comment { get; set; }
        public List<DbcSignal> Signals { get; set; }
        public bool IsExtended { get; set; }
        public bool IsRemoteFrame { get; set; } = false;
        public int? IntervalMs { get; set; }

        public DbcMessage()
        {
            Name = string.Empty;
            Transmitter = string.Empty;
            Comment = string.Empty;
            Signals = new List<DbcSignal>();
        }

        /// <summary>
        /// Serialize message to bytes
        /// Layout: IdType(u8) | MsgId(u32) | DLC(u8) | Periodicity(u16) | MtoEnable(u8) | MtoAction(u8) | MsgTimeout(u16) | RxSignalCnt(u8) | RxSignalOffset(u32)
        /// </summary>
        public byte[] ToBytes(bool tx = false)
        {
            // Helper methods to write numbers in big-endian
            void WriteUshort(BinaryWriter writer, ushort value)
            {
                var bytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                writer.Write(bytes);
            }

            void WriteUint(BinaryWriter writer, uint value)
            {
                var bytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                writer.Write(bytes);
            }

            if (tx)
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)(IsExtended ? 1 : 0));      // IdType        (uint8_t)  — 0:Standard, 1:Extended
                    WriteUint(writer, IsExtended ? (uint)Id - 0x80000000 : (uint)Id);                   // MsgId         (uint32_t)
                    writer.Write((byte)DLC);                       // DLC           (uint8_t)
                    WriteUshort(writer, (ushort)(IntervalMs ?? 0));// Periodicity   (uint16_t) — 0 if not periodic
                    writer.Write((byte)0);   // MTO Enable    (uint8_t)  — mapped from IsRemoteFrame/Enable flag
                    writer.Write((byte)(IsRemoteFrame ? 1 : 0));                         // MTOAction     (uint8_t)  — Action Type 20 (as per protocol)
                    //WriteUshort(writer, (ushort)0);                // MsgTmOt       (uint16_t) — 5 ms default
                    writer.Write((byte)0);                         // RxSignalCnt   (uint8_t)  — number of signals in this message
                    WriteUint(writer, (uint)0);                    // RxSignalOffset(uint32_t) — filled by caller after layout is known
                    return ms.ToArray();
                }
            else
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)(IsExtended ? 1 : 0));      // IdType        (uint8_t)  — 0:Standard, 1:Extended
                    WriteUint(writer, IsExtended ? (uint)Id - 0x80000000 : (uint)Id);                   // MsgId         (uint32_t)
                    writer.Write((byte)DLC);                       // DLC           (uint8_t)
                    WriteUshort(writer, (ushort)(IntervalMs ?? 0));// Periodicity   (uint16_t) — 0 if not periodic
                    writer.Write((byte)0);                         // MTO Enable    (uint8_t)  — Message Timeout Enable flag
                    writer.Write((byte)(IsRemoteFrame ? 1 : 0));                         // MTOAction     (uint8_t)  — Action Type 20 (as per protocol)
                    WriteUshort(writer, (ushort)0);                // MsgTmOt       (uint16_t) — 5 ms default
                    writer.Write((byte)Signals.Count);             // txSignalCnt   (uint8_t)  — number of signals in this message
                    WriteUint(writer, (uint)0);                    // txSignalOffset(uint32_t) — filled by caller after layout is known
                    return ms.ToArray();
                }
        }

        public string ToHexValue(bool tx = false)
        {
            return string.Join(" ", ToBytes(tx).Select(b => $"0x{b:X2}")) + Environment.NewLine;
        }

    }

    /// <summary>
    /// Value table for signal enumeration
    /// </summary>
    public class ValueTable
    {
        public string Name { get; set; }
        public Dictionary<int, string> Values { get; set; }

        public ValueTable()
        {
            Name = string.Empty;
            Values = new Dictionary<int, string>();
        }

        /// <summary>
        /// Serialize value table to bytes — protocol to be defined
        /// </summary>
        public byte[] ToBytes()
        {
            // TODO: Define serialization protocol
            throw new NotImplementedException("ToBytes protocol not yet defined.");
        }
    }

    #endregion

    #region DBC Database

    /// <summary>
    /// Direction of a CAN message relative to the local node
    /// </summary>
    public enum MessageDirection
    {
        Tx,       // This node transmits the message
        Rx,       // This node receives the message
        Unknown   // Transmitter not matched to any known node (e.g. Vector__XXX with no local node set)
    }

    /// <summary>
    /// Complete DBC database — messages, signals, nodes, value tables, attributes
    /// </summary>
    public class DbcDatabase
    {
        public CanBaudrate CBaudrate { get; set; }
        public string Version { get; set; }
        //public List<string> Nodes { get; set; }
        public Dictionary<uint, DbcMessage> Messages { get; set; }
        //public Dictionary<string, ValueTable> ValueTables { get; set; }
        //public Dictionary<string, string> Attributes { get; set; }
        public List<string> ParseErrors { get; set; }

        public DbcDatabase()
        {
            Version = string.Empty;
            //Nodes = new List<string>();
            Messages = new Dictionary<uint, DbcMessage>();
            //ValueTables = new Dictionary<string, ValueTable>();
            //Attributes = new Dictionary<string, string>();
            ParseErrors = new List<string>();

        }

        /// <summary>
        /// Get message by CAN ID
        /// </summary>
        public DbcMessage GetMessage(uint id)
        {
            return Messages.ContainsKey(id) ? Messages[id] : null;
        }

        /// <summary>
        /// Get message by name
        /// </summary>
        public DbcMessage GetMessageByName(string name)
        {
            return Messages.Values.FirstOrDefault(m => m.Name == name);
        }

        /// <summary>
        /// Returns all Tx messages for the given local node.
        /// If localNodeName is null, returns messages whose transmitter is a real (non-placeholder) node.
        /// </summary>
        public IEnumerable<DbcMessage> GetTxMessages()
        {
            return Messages.Values.Where(m => m.IsRemoteFrame && m.Signals.Any(s => s.IsSelected));
        }

        /// <summary>
        /// Returns all Rx messages for the given local node.
        /// Requires localNodeName to distinguish Rx from Unknown.
        /// </summary>

        public IEnumerable<DbcMessage> GetRxMessages()
        {
            return Messages.Values.Where(m => m.Signals.Any(s => s.IsSelected));
        }

        // ── Binary layout constants ─────────────────────────────────────────────
        // DbcMessage.ToBytes(false) Rx path  = 17 bytes (includes MsgTmOt ushort)
        // DbcMessage.ToBytes(true)  Tx path  = 15 bytes (MsgTmOt commented-out)
        // DbcSignal.ToBytes()                = 13 bytes (5 id/type fields + 4 factor + 4 offset)
        private const int RX_MSG_BLOCK_BYTES  = 17;
        private const int TX_MSG_BLOCK_BYTES  = 15;
        private const int SIGNAL_BLOCK_BYTES  = 13;

        // ── PortData: pre-computed per-port message lists and signal offsets ──
        private readonly struct PortData
        {
            public readonly List<DbcMessage> RxMessages;
            public readonly List<DbcMessage> TxMessages;
            /// <summary>Relative byte offset of each Rx message's first signal within this port's signal block.</summary>
            public readonly List<int> RxSignalOffsets;

            /// <summary>Byte size of all Rx message blocks for this port.</summary>
            public int RxMsgBlockSize => RxMessages.Count * RX_MSG_BLOCK_BYTES;
            /// <summary>Byte size of all Tx message blocks for this port.</summary>
            public int TxMsgBlockSize => TxMessages.Count * TX_MSG_BLOCK_BYTES;
            /// <summary>Byte size of all Rx signal blocks for this port.</summary>
            public int RxSignalBlockSize => RxMessages.Sum(m => m.Signals.Count) * SIGNAL_BLOCK_BYTES;

            public PortData(DbcDatabase db)
            {
                RxMessages = db.GetRxMessages().ToList();
                TxMessages = db.GetTxMessages().ToList();

                // Signal IDs are already assigned by the caller (CircuitCommandHandler)
                // using a single counter spanning all 3 ports before calling BuildMultiPortPayload.

                RxSignalOffsets = new List<int>();
                int off = 0;
                foreach (var msg in RxMessages)
                {
                    RxSignalOffsets.Add(off);
                    off += msg.Signals.Count * SIGNAL_BLOCK_BYTES;
                }
            }
        }

        // ── WritePort: writes one 12-byte port header ────────────────────────
        private static void WritePort(BinaryWriter writer, DbcDatabase? db,
            uint rxMsgCfgOffset, uint txMsgCfgOffset)
        {
            bool isActive = db != null && db.Messages.Count > 0;
            var rxMessages = isActive ? db!.GetRxMessages().ToList() : new List<DbcMessage>();
            var txMessages = isActive ? db!.GetTxMessages().ToList() : new List<DbcMessage>();

            // PortEnable
            writer.Write((byte)(isActive ? 1 : 0));
            // Baudrate
            writer.Write((byte)(isActive ? (int)db!.CBaudrate : 0));
            // RxMsgCount
            writer.Write((byte)rxMessages.Count);
            // TxMsgCount
            writer.Write((byte)txMessages.Count);

            // RxMsgCfgOffset (big-endian uint32)
            var rxOff = BitConverter.GetBytes(isActive ? rxMsgCfgOffset : 0u);
            if (BitConverter.IsLittleEndian) Array.Reverse(rxOff);
            writer.Write(rxOff);

            // TxMsgCfgOffset (big-endian uint32)
            var txOff = BitConverter.GetBytes(isActive ? txMsgCfgOffset : 0u);
            if (BitConverter.IsLittleEndian) Array.Reverse(txOff);
            writer.Write(txOff);
        }

        // ── WritePortBlocks: writes Rx message blocks, Tx message blocks, Rx signal blocks ─
        private static void WritePortBlocks(BinaryWriter writer, PortData pd)
        {
            // Rx message blocks — patch the last 4 bytes (RxSignalOffset) with the relative signal offset
            for (int i = 0; i < pd.RxMessages.Count; i++)
            {
                byte[] msgBytes = pd.RxMessages[i].ToBytes();
                byte[] offsetBytes = BitConverter.GetBytes((uint)pd.RxSignalOffsets[i]);
                if (BitConverter.IsLittleEndian) Array.Reverse(offsetBytes);
                Array.Copy(offsetBytes, 0, msgBytes, msgBytes.Length - 4, 4);
                writer.Write(msgBytes);
            }

            // Tx message blocks
            foreach (var msg in pd.TxMessages)
                writer.Write(msg.ToBytes(true));

            // Rx signal blocks (in message order)
            foreach (var msg in pd.RxMessages)
                foreach (var sig in msg.Signals)
                    writer.Write(sig.ToBytes());
        }

        /// <summary>
        /// Serializes up to 3 DBC databases into a single multi-port binary payload.
        ///
        /// Binary layout:
        ///   [36 bytes: 3 port headers × 12 bytes each]
        ///   [Port 1 Rx message blocks (RX_MSG_BLOCK_BYTES each)]
        ///   [Port 1 Tx message blocks (TX_MSG_BLOCK_BYTES each)]
        ///   [Port 1 Rx signal blocks  (SIGNAL_BLOCK_BYTES each)]
        ///   [Port 2 Rx message blocks]
        ///   [Port 2 Tx message blocks]
        ///   [Port 2 Rx signal blocks]
        ///   [Port 3 Rx message blocks]
        ///   [Port 3 Tx message blocks]
        ///   [Port 3 Rx signal blocks]
        ///
        /// IMPORTANT: Signal IDs (DbcSignal.SingalId) must already be assigned by the
        /// caller (CircuitCommandHandler) using a single counter that increments
        /// sequentially across Port1 → Port2 → Port3 without resetting.
        /// Range: 31–255; 0 = unselected.  This method only serialises — it does NOT
        /// assign signal IDs.
        /// </summary>
        public static byte[] BuildMultiPortPayload(
            DbcDatabase? port1,
            DbcDatabase? port2,
            DbcDatabase? port3)
        {
            const int PORT_HEADER_SIZE   = 12; // 1+1+1+1+4+4
            const int TOTAL_HEADER_SIZE  = 3 * PORT_HEADER_SIZE; // 36 bytes

            // Pre-compute per-port data (null db = inactive port)
            var p1 = port1 != null ? (PortData?)new PortData(port1) : null;
            var p2 = port2 != null ? (PortData?)new PortData(port2) : null;
            var p3 = port3 != null ? (PortData?)new PortData(port3) : null;

            // Calculate message-block offsets from start of payload
            // Layout: [36 header bytes] [p1 rx msgs] [p1 tx msgs] [p1 rx sigs]
            //                           [p2 rx msgs] [p2 tx msgs] [p2 rx sigs]
            //                           [p3 rx msgs] [p3 tx msgs] [p3 rx sigs]
            int cursor = TOTAL_HEADER_SIZE;

            uint p1RxOff = p1.HasValue && p1.Value.RxMessages.Count > 0 ? (uint)cursor : 0u;
            cursor += p1?.RxMsgBlockSize ?? 0;
            uint p1TxOff = p1.HasValue && p1.Value.TxMessages.Count > 0 ? (uint)cursor : 0u;
            cursor += p1?.TxMsgBlockSize ?? 0;
            cursor += p1?.RxSignalBlockSize ?? 0;

            uint p2RxOff = p2.HasValue && p2.Value.RxMessages.Count > 0 ? (uint)cursor : 0u;
            cursor += p2?.RxMsgBlockSize ?? 0;
            uint p2TxOff = p2.HasValue && p2.Value.TxMessages.Count > 0 ? (uint)cursor : 0u;
            cursor += p2?.TxMsgBlockSize ?? 0;
            cursor += p2?.RxSignalBlockSize ?? 0;

            uint p3RxOff = p3.HasValue && p3.Value.RxMessages.Count > 0 ? (uint)cursor : 0u;
            cursor += p3?.RxMsgBlockSize ?? 0;
            uint p3TxOff = p3.HasValue && p3.Value.TxMessages.Count > 0 ? (uint)cursor : 0u;

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // ── 3 port headers (12 bytes each) ──────────────────────────────────
            WritePort(writer, port1, p1RxOff, p1TxOff);
            WritePort(writer, port2, p2RxOff, p2TxOff);
            WritePort(writer, port3, p3RxOff, p3TxOff);

            // ── Port 1 message + signal blocks ──────────────────────────────────
            if (p1.HasValue) WritePortBlocks(writer, p1.Value);

            // ── Port 2 message + signal blocks ──────────────────────────────────
            if (p2.HasValue) WritePortBlocks(writer, p2.Value);

            // ── Port 3 message + signal blocks ──────────────────────────────────
            if (p3.HasValue) WritePortBlocks(writer, p3.Value);

            return ms.ToArray();
        }

        // ── Convenience wrapper kept for DbcDatabaseEditor.DownloadHex ──────
        /// <summary>Treats this database as port1 only; ports 2 and 3 are inactive.</summary>
        public string ToHexValue() => BuildMultiPortHex(this, null, null);

        /// <summary>
        /// Returns a human-readable hex dump of a multi-port payload (for diagnostics / download).
        /// </summary>
        public static string BuildMultiPortHex(
            DbcDatabase? port1,
            DbcDatabase? port2,
            DbcDatabase? port3)
        {
            var sb = new System.Text.StringBuilder();
            var ports = new[] { port1, port2, port3 };

            // Simplified offsets for hex preview (all ports relative to 36-byte header block)
            const int HEADER_BLOCK = 3 * 12;

            for (int p = 0; p < 3; p++)
            {
                var db = ports[p];
                bool isActive = db != null && db.Messages.Count > 0;
                sb.AppendLine($"--- Port{p + 1} Header ---");

                if (isActive)
                {
                    var rxMessages = db!.GetRxMessages().ToList();
                    var txMessages = db!.GetTxMessages().ToList();

                    int rxMsgCfgOffset = HEADER_BLOCK;
                    int txMsgCfgOffset = rxMsgCfgOffset + rxMessages.Count * RX_MSG_BLOCK_BYTES;

                    sb.AppendLine($"{1:X2} {(int)db.CBaudrate:X2} {rxMessages.Count:X2} {txMessages.Count:X2} {rxMsgCfgOffset:X8} {txMsgCfgOffset:X8}");

                    sb.AppendLine($"--- Port{p + 1} RX Messages ---");
                    foreach (var msg in rxMessages)
                        sb.AppendLine(msg.ToHexValue(false));

                    sb.AppendLine($"--- Port{p + 1} TX Messages ---");
                    foreach (var msg in txMessages)
                        sb.AppendLine(msg.ToHexValue(true));

                    sb.AppendLine($"--- Port{p + 1} RX Signals ---");
                    foreach (var msg in rxMessages)
                        foreach (var sig in msg.Signals)
                            sb.AppendLine(sig.ToHexValue(false));
                }
                else
                {
                    sb.AppendLine($"{0:X2} {0:X2} {0:X2} {0:X2} {0:X8} {0:X8}");
                }
            }

            return sb.ToString();
        }
    }

    #endregion

    #region DBC Parser

    /// <summary>
    /// Parses .dbc files into a DbcDatabase.
    /// All parse methods are safe — no exceptions are thrown.
    /// Malformed lines are recorded in DbcDatabase.ParseErrors and skipped.
    /// </summary>
    public class DbcParser
    {

        public DbcDatabase Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                var empty = new DbcDatabase();
                empty.ParseErrors.Add($"File not found or path is empty: '{filePath}'");
                return empty;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception ex)
            {
                var empty = new DbcDatabase();
                empty.ParseErrors.Add($"Failed to read file '{filePath}': {ex.Message}");
                return empty;
            }

            var db = ParseLines(lines);


            return db;
        }

        public DbcDatabase ParseContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                var empty = new DbcDatabase();
                empty.ParseErrors.Add("ParseContent called with null or empty content.");
                return empty;
            }

            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var db = ParseLines(lines);


            return db;
        }

        private DbcDatabase ParseLines(string[] lines)
        {
            var db = new DbcDatabase();
            var messageComments = new Dictionary<uint, string>();
            var signalComments = new Dictionary<string, string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                try
                {
                    if (line.StartsWith("VERSION"))
                        db.Version = ExtractQuotedString(line);

                    else if (line.StartsWith("NS_ ") || line.EndsWith("_"))
                    { /* symbol section — ignored */ }

                    //else if (line.StartsWith("BU_:"))
                    //    ParseNodes(line, db);

                    //else if (line.StartsWith("VAL_TABLE_"))
                    //    ParseValueTable(line, db);

                    else if (line.StartsWith("BO_ "))
                        ParseMessage(line, lines, ref i, db);

                    else if (line.StartsWith("CM_ BO_ "))
                        ParseMessageComment(line, messageComments, db);

                    //else if (line.StartsWith("CM_ SG_ "))
                    //    ParseSignalComment(line, signalComments, db);

                    //else if (line.StartsWith("BA_ "))
                    //    ParseAttribute(line, db);
                }
                catch (Exception ex)
                {
                    // Should never reach here given all parse methods are safe,
                    // but belt-and-suspenders: record and continue.
                    db.ParseErrors.Add($"[Line {i + 1}] Unexpected error: {ex.Message} — Line: \"{line}\"");
                }
            }

            // Apply message comments
            foreach (var kvp in messageComments)
                if (db.Messages.ContainsKey(kvp.Key))
                    db.Messages[kvp.Key].Comment = kvp.Value;

            // Apply signal comments
            foreach (var kvp in signalComments)
            {
                int sep = kvp.Key.IndexOf('_');
                if (sep > 0 &&
                    uint.TryParse(kvp.Key.Substring(0, sep), out uint msgId) &&
                    db.Messages.ContainsKey(msgId))
                {
                    string sigName = kvp.Key.Substring(sep + 1);
                    var sig = db.Messages[msgId].Signals.FirstOrDefault(s => s.Name == sigName);
                    if (sig != null)
                        sig.Comment = kvp.Value;
                }
            }

            return db;
        }

        // ── Parse methods — all return void, record errors, never throw ──────────

        //private void ParseNodes(string line, DbcDatabase db)
        //{
        //    // BU_: Node1 Node2 Node3
        //    if (line.Length <= 4)
        //        return;

        //    string nodesPart = line.Substring(4).Trim();
        //    if (!string.IsNullOrEmpty(nodesPart))
        //        db.Nodes.AddRange(nodesPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        //}

        //private void ParseValueTable(string line, DbcDatabase db)
        //{
        //    // VAL_TABLE_ TableName 0 "Value0" 1 "Value1" ;
        //    var match = Regex.Match(line, @"VAL_TABLE_\s+(\w+)\s+(.+);");
        //    if (!match.Success)
        //    {
        //        db.ParseErrors.Add($"VAL_TABLE_ line could not be parsed: \"{line}\"");
        //        return;
        //    }

        //    string tableName = match.Groups[1].Value;
        //    var table = new ValueTable { Name = tableName };

        //    foreach (Match vm in Regex.Matches(match.Groups[2].Value, @"(\d+)\s+""([^""]+)"""))
        //    {
        //        if (int.TryParse(vm.Groups[1].Value, out int key))
        //            table.Values[key] = vm.Groups[2].Value;
        //        else
        //            db.ParseErrors.Add($"VAL_TABLE_ '{tableName}': invalid key '{vm.Groups[1].Value}' — skipped.");
        //    }

        //    db.ValueTables[tableName] = table;
        //}

        private void ParseMessage(string line, string[] lines, ref int index, DbcDatabase db)
        {
            // BO_ 2147483649 CellVol01_04: 8 Vector__XXX
            var match = Regex.Match(line, @"BO_\s+(\d+)\s+(\w+):\s*(\d+)\s+(\w+)");
            if (!match.Success)
            {
                db.ParseErrors.Add($"BO_ line could not be parsed: \"{line}\"");
                return;
            }

            if (!uint.TryParse(match.Groups[1].Value, out uint id))
            {
                db.ParseErrors.Add($"BO_ line has invalid ID '{match.Groups[1].Value}': \"{line}\"");
                return;
            }

            if (!int.TryParse(match.Groups[3].Value, out int dlc))
            {
                db.ParseErrors.Add($"BO_ line has invalid DLC '{match.Groups[3].Value}': \"{line}\"");
                return;
            }

            var message = new DbcMessage
            {
                Id = id,
                Name = match.Groups[2].Value,
                DLC = dlc,
                Transmitter = match.Groups[4].Value,
                IsExtended = id > 0x80000000
            };

            // Parse signals belonging to this message
            index++;
            while (index < lines.Length)
            {
                string signalLine = lines[index].Trim();
                if (string.IsNullOrEmpty(signalLine) || !signalLine.StartsWith("SG_"))
                    break;

                ParseSignal(signalLine, message, db);
                index++;
            }
            index--; // Adjust for outer loop increment

            db.Messages[id] = message;
        }

        private void ParseSignal(string line, DbcMessage message, DbcDatabase db)
        {
            // SG_ CellVol01 : 7|16@0+ (0.0001,0) [0|4.2] "V" Vector__XXX
            var match = Regex.Match(line,
                @"SG_\s+(\w+)\s*:\s*(\d+)\|(\d+)@([01])([+-])\s*\(([^,]+),([^)]+)\)\s*\[([^|]+)\|([^\]]+)\]\s*""([^""]*)""\s*(.+)");

            if (!match.Success)
            {
                db.ParseErrors.Add($"SG_ line in message '{message.Name}' could not be parsed: \"{line}\"");
                return;
            }

            if (!int.TryParse(match.Groups[2].Value, out int startBit))
            {
                db.ParseErrors.Add($"SG_ '{match.Groups[1].Value}': invalid StartBit '{match.Groups[2].Value}' — skipped.");
                return;
            }

            if (!int.TryParse(match.Groups[3].Value, out int bitLength))
            {
                db.ParseErrors.Add($"SG_ '{match.Groups[1].Value}': invalid BitLength '{match.Groups[3].Value}' — skipped.");
                return;
            }

            // Use InvariantCulture so "0.0001" parses correctly regardless of system locale
            if (!double.TryParse(match.Groups[6].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double factor))
            {
                db.ParseErrors.Add($"SG_ '{match.Groups[1].Value}': invalid Factor '{match.Groups[6].Value}' — defaulting to 1.");
                factor = 1.0;
            }

            if (!double.TryParse(match.Groups[7].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double offset))
            {
                db.ParseErrors.Add($"SG_ '{match.Groups[1].Value}': invalid Offset '{match.Groups[7].Value}' — defaulting to 0.");
                offset = 0.0;
            }

            if (!double.TryParse(match.Groups[8].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double minimum))
            {
                db.ParseErrors.Add($"SG_ '{match.Groups[1].Value}': invalid Minimum '{match.Groups[8].Value}' — defaulting to 0.");
                minimum = 0.0;
            }

            if (!double.TryParse(match.Groups[9].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double maximum))
            {
                db.ParseErrors.Add($"SG_ '{match.Groups[1].Value}': invalid Maximum '{match.Groups[9].Value}' — defaulting to 0.");
                maximum = 0.0;
            }

            var signal = new DbcSignal
            {
                Name = match.Groups[1].Value,
                StartBit = startBit,
                BitLength = bitLength,
                ByteOrder = match.Groups[4].Value == "0" ? ByteOrder.Motorola : ByteOrder.Intel,
                ValueType = match.Groups[5].Value == "+" ? DbcValueType.Unsigned : DbcValueType.Signed,
                Factor = factor,
                Offset = offset,
                Minimum = minimum,
                Maximum = maximum,
                Unit = match.Groups[10].Value
            };

            message.Signals.Add(signal);
        }

        private void ParseMessageComment(string line, Dictionary<uint, string> comments, DbcDatabase db)
        {
            // CM_ BO_ 2147483649 "Cell voltages 1 to 4";
            var match = Regex.Match(line, @"CM_\s+BO_\s+(\d+)\s+""([^""]*)""\s*;");
            if (!match.Success)
            {
                db.ParseErrors.Add($"CM_ BO_ line could not be parsed: \"{line}\"");
                return;
            }

            if (!uint.TryParse(match.Groups[1].Value, out uint id))
            {
                db.ParseErrors.Add($"CM_ BO_ line has invalid ID '{match.Groups[1].Value}' — skipped.");
                return;
            }

            comments[id] = match.Groups[2].Value;
        }

        //private void ParseSignalComment(string line, Dictionary<string, string> comments, DbcDatabase db)
        //{
        //    // CM_ SG_ 2147483649 CellVol01 "Cell voltage 1";
        //    var match = Regex.Match(line, @"CM_\s+SG_\s+(\d+)\s+(\w+)\s+""([^""]*)""\s*;");
        //    if (!match.Success)
        //    {
        //        db.ParseErrors.Add($"CM_ SG_ line could not be parsed: \"{line}\"");
        //        return;
        //    }

        //    // Key format: "msgId_signalName" — use first underscore as separator in consumer
        //    comments[$"{match.Groups[1].Value}_{match.Groups[2].Value}"] = match.Groups[3].Value;
        //}

        //private void ParseAttribute(string line, DbcDatabase db)
        //{
        //    // BA_ "VFrameFormat" BO_ 2147483649 1;
        //    var match = Regex.Match(line, @"BA_\s+""([^""]+)""\s+(.+);");
        //    if (!match.Success)
        //    {
        //        db.ParseErrors.Add($"BA_ line could not be parsed: \"{line}\"");
        //        return;
        //    }

        //    db.Attributes[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        //}

        private string ExtractQuotedString(string line)
        {
            var match = Regex.Match(line, @"""([^""]*)""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }

    #endregion

}
