using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HydraX.Library
{
    public partial class BlackOps3
    {
        /// <summary>
        /// Black Ops 3 Font Icon Logic
        /// </summary>
        private class FontIcon : IAssetPool
        {
            #region AssetStructures

            /// <summary>
            /// FontIcon Asset Structure (sizeof=0x20)
            /// </summary>
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private struct FontIconAsset
            {
                public long NamePointer;        // 0x00 - const char *name
                public int NumEntries;          // 0x08
                public int NumAliasEntries;     // 0x0C
                public long FontIconEntryPtr;   // 0x10 - FontIconEntry *fontIconEntry
                public long FontIconAliasPtr;   // 0x18 - FontIconAlias *fontIconAlias
            }

            /// <summary>
            /// FontIconEntry Structure (sizeof=0x28)
            /// </summary>
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private struct FontIconEntry
            {
                // FontIconName (sizeof=0x10)
                public long FontIconNameStringPtr; // 0x00 - const char *string
                public int FontIconNameHash;       // 0x08
                public int NamePadding;            // 0x0C

                public long FontIconMaterialHandle; // 0x10 - MaterialHandle
                public int FontIconSize;            // 0x18
                public float XScale;               // 0x1C
                public float YScale;               // 0x20
                public int EntryPadding;            // 0x24
            }

            /// <summary>
            /// FontIconAlias Structure (sizeof=0x8)
            /// </summary>
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private struct FontIconAlias
            {
                public int AliasHash;   // 0x00
                public int ButtonHash;  // 0x04
            }

            #endregion

            /// <summary>
            /// Size of each asset
            /// </summary>
            public int AssetSize { get; set; }

            /// <summary>
            /// Gets or Sets the number of Assets
            /// </summary>
            public int AssetCount { get; set; }

            /// <summary>
            /// Gets or Sets the Start Address
            /// </summary>
            public long StartAddress { get; set; }

            /// <summary>
            /// Gets or Sets the End Address
            /// </summary>
            public long EndAddress { get { return StartAddress + (AssetCount * AssetSize); } set => throw new NotImplementedException(); }

            /// <summary>
            /// Gets the Name of this Pool
            /// </summary>
            public string Name => "fonticon";

            /// <summary>
            /// Gets the Setting Group for this Pool
            /// </summary>
            public string SettingGroup => "UI";

            /// <summary>
            /// Gets the Index of this Pool
            /// </summary>
            public int Index => (int)AssetPool.fonticon;

            /// <summary>
            /// Loads Assets from this Asset Pool
            /// </summary>
            public List<Asset> Load(HydraInstance instance)
            {
                var results = new List<Asset>();

                var poolInfo = instance.Reader.ReadStruct<AssetPoolInfo>(instance.Game.AssetPoolsAddress + (Index * 0x20));

                StartAddress = poolInfo.PoolPointer;
                AssetSize = poolInfo.AssetSize;
                AssetCount = poolInfo.PoolSize;

                for (int i = 0; i < AssetCount; i++)
                {
                    var header = instance.Reader.ReadStruct<FontIconAsset>(StartAddress + (i * AssetSize));

                    if (IsNullAsset(header.NamePointer))
                        continue;

                    var address = StartAddress + (i * AssetSize);

                    results.Add(new Asset()
                    {
                        Name = instance.Reader.ReadNullTerminatedString(header.NamePointer),
                        Type = Name,
                        Status = "Loaded",
                        Data = address,
                        LoadMethod = ExportAsset,
                        Zone = ((BlackOps3)instance.Game).ZoneNames[address],
                        Information = string.Format("Entries: {0} - Aliases: {1}", header.NumEntries, header.NumAliasEntries)
                    });
                }

                return results;
            }

            /// <summary>
            /// Exports the given asset from this pool
            /// </summary>
            public void ExportAsset(Asset asset, HydraInstance instance)
            {
                var header = instance.Reader.ReadStruct<FontIconAsset>((long)asset.Data);

                if (asset.Name != instance.Reader.ReadNullTerminatedString(header.NamePointer))
                    throw new Exception("The asset at the expected memory address has changed. Press the Load Game button to refresh the asset list.");

                // Output as CSV: fonticons/<name>.csv
                string path = Path.Combine("exported_files", instance.Game.Name, asset.Name);
                if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    path = Path.ChangeExtension(path, ".csv");

                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var result = new StringBuilder();

                // ---- Entries ----
                int entrySize = Marshal.SizeOf<FontIconEntry>(); // 0x28 = 40 bytes
                for (int i = 0; i < header.NumEntries; i++)
                {
                    var entry = instance.Reader.ReadStruct<FontIconEntry>(header.FontIconEntryPtr + (i * entrySize));

                    string iconName = entry.FontIconNameStringPtr != 0
                                            ? instance.Reader.ReadNullTerminatedString(entry.FontIconNameStringPtr)
                                            : string.Format("0x{0:X8}", entry.FontIconNameHash);
                    string materialName = entry.FontIconMaterialHandle != 0
                                            ? instance.Reader.ReadNullTerminatedString(instance.Reader.ReadInt64(entry.FontIconMaterialHandle))
                                            : "";

                    result.AppendLine(string.Format("{0},{1},{2},{3},{4}",
                        iconName,
                        entry.FontIconSize,
                        materialName,
                        entry.XScale,
                        entry.YScale));
                }

                // ---- Aliases ----
                int aliasSize = Marshal.SizeOf<FontIconAlias>(); // 0x8 = 8 bytes
                for (int i = 0; i < header.NumAliasEntries; i++)
                {
                    var alias = instance.Reader.ReadStruct<FontIconAlias>(header.FontIconAliasPtr + (i * aliasSize));

                    result.AppendLine(string.Format("Alias,0x{0:X8},0x{1:X8}",
                        alias.AliasHash,
                        alias.ButtonHash));
                }

                File.WriteAllText(path, result.ToString());
            }

            /// <summary>
            /// Checks if the given asset is a null slot
            /// </summary>
            public bool IsNullAsset(Asset asset)
            {
                return IsNullAsset((long)asset.Data);
            }

            /// <summary>
            /// Checks if the given asset is a null slot
            /// </summary>
            public bool IsNullAsset(long nameAddress)
            {
                return nameAddress >= StartAddress && nameAddress <= EndAddress || nameAddress == 0;
            }
        }
    }
}