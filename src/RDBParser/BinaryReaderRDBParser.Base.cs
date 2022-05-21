﻿using System.IO;

namespace RDBParser
{
    public partial class BinaryReaderRDBParser
    {
        public void ReadObject(BinaryReader br, byte[] key, int encType, long expiry, Info info)
        {
            if (encType == Constant.DataType.STRING)
            {
                var value = br.ReadStr();

                info.Encoding = Constant.ObjEncoding.STRING;
                _callback.Set(key, value, expiry, info);
            }
            else if (encType == Constant.DataType.LIST)
            {
                var length = br.ReadLength();
                info.Encoding = Constant.ObjEncoding.LINKEDLIST;
                _callback.StartList(key, expiry, info);
                while (length > 0)
                {
                    length--;
                    var val = br.ReadStr();
                    _callback.RPush(key, val);
                }
                _callback.EndList(key, info);
            }
            else if (encType == Constant.DataType.SET)
            {
                var cardinality = br.ReadLength();
                info.Encoding = Constant.ObjEncoding.HT;
                _callback.StartSet(key, (long)cardinality, expiry, info);
                while (cardinality > 0)
                {
                    cardinality--;
                    var member = br.ReadStr();
                    _callback.SAdd(key, member);
                }
                _callback.EndSet(key);
            }
            else if (encType == Constant.DataType.ZSET || encType == Constant.DataType.ZSET_2)
            {
                var cardinality = br.ReadLength();
                info.Encoding = Constant.ObjEncoding.SKIPLIST;
                _callback.StartSortedSet(key, (long)cardinality, expiry, info);
                while (cardinality > 0)
                {
                    cardinality--;
                    var member = br.ReadStr();

                    double score = encType == Constant.DataType.ZSET_2
                        ? br.ReadDouble()
                        : br.ReadFloat();

                    _callback.ZAdd(key, score, member);
                }
                _callback.EndSortedSet(key);
            }
            else if (encType == Constant.DataType.HASH)
            {
                var length = br.ReadLength();

                info.Encoding = Constant.ObjEncoding.HT;
                _callback.StartHash(key, (long)length, expiry, info);

                while (length > 0)
                {
                    length--;
                    var field = br.ReadStr();
                    var value = br.ReadStr();

                    _callback.HSet(key, field, value);
                }
                _callback.EndHash(key);
            }
            else if (encType == Constant.DataType.HASH_ZIPMAP)
            {
                ReadZipMap(br);
            }
            else if (encType == Constant.DataType.LIST_ZIPLIST)
            {
                ReadZipList(br);
            }
            else if (encType == Constant.DataType.SET_INTSET)
            {
                ReadIntSet(br);
            }
            else if (encType == Constant.DataType.ZSET_ZIPLIST)
            {
                ReadZSetFromZiplist(br);
            }
            else if (encType == Constant.DataType.HASH_ZIPLIST)
            {
                ReadHashFromZiplist(br);
            }
            else if (encType == Constant.DataType.LIST_QUICKLIST
                || encType == Constant.DataType.LIST_QUICKLIST_2)
            {
                ReadListFromQuickList(br, encType);
            }
            else if (encType == Constant.DataType.MODULE)
            {
                throw new RDBParserException($"Unable to read Redis Modules RDB objects (key {key})");
            }
            else if (encType == Constant.DataType.MODULE_2)
            {
                ReadModule(br);
            }
            else if (encType == Constant.DataType.STREAM_LISTPACKS
                || encType == Constant.DataType.STREAM_LISTPACKS_2)
            {
                ReadStream(br, encType);
            }
            else if (encType == Constant.DataType.HASH_LISTPACK)
            {
                ReadHashFromListPack(br);
            }
            else if (encType == Constant.DataType.ZSET_LISTPACK)
            {
                ReadZSetFromListPack(br);
            }
            else
            {
                throw new RDBParserException($"Invalid object type {encType} for {key} ");
            }
        }

        private void SkipObject(BinaryReader br, int encType)
        {
            var skip = 0;

            if (encType == Constant.DataType.STRING)
            {
                skip = 1;
            }
            else if (encType == Constant.DataType.LIST)
            {
                var length = br.ReadLength();
                skip = (int)length;
            }
            else if (encType == Constant.DataType.SET)
            {
                var length = br.ReadLength();
                skip = (int)length;
            }
            else if (encType == Constant.DataType.ZSET || encType == Constant.DataType.ZSET_2)
            {
                var length = br.ReadLength();
                while (length > 0)
                {
                    length--;
                    br.SkipStr();
                    double score = encType == Constant.DataType.ZSET_2
                        ? br.ReadDouble()
                        : br.ReadFloat();
                }
            }
            else if (encType == Constant.DataType.HASH)
            {
                var length = br.ReadLength();
                skip = (int)length * 2;
            }
            else if (encType == Constant.DataType.HASH_ZIPMAP)
            {
                skip = 1;
            }
            else if (encType == Constant.DataType.LIST_ZIPLIST)
            {
                skip = 1;
            }
            else if (encType == Constant.DataType.SET_INTSET)
            {
                skip = 1;
            }
            else if (encType == Constant.DataType.ZSET_ZIPLIST)
            {
                skip = 1;
            }
            else if (encType == Constant.DataType.HASH_ZIPLIST)
            {
                skip = 1;
            }
            else if (encType == Constant.DataType.LIST_QUICKLIST
                || encType == Constant.DataType.LIST_QUICKLIST_2)
            {
                var length = br.ReadLength();

                skip = (int)length;

                if (encType == Constant.DataType.LIST_QUICKLIST_2)
                {
                    while (length > 0)
                    {
                        br.ReadLength();
                        length--;
                    }
                }
            }
            else if (encType == Constant.DataType.MODULE)
            {
                throw new RDBParserException($"Unable to read Redis Modules RDB objects (key {_key})");
            }
            else if (encType == Constant.DataType.MODULE_2)
            {
                SkipModule(br);
            }
            else if (encType == Constant.DataType.STREAM_LISTPACKS
                || encType == Constant.DataType.STREAM_LISTPACKS_2)
            {
                SkipStream(br, encType);
            }
            else if (encType == Constant.DataType.HASH_LISTPACK)
            {
                skip = 1;
            }
            else if (encType == Constant.DataType.ZSET_LISTPACK)
            {
                skip = 1;
            }
            else
            {
                throw new RDBParserException($"Invalid object type {encType} for {_key} ");
            }

            for (int i = 0; i < skip; i++)
            {
                br.SkipStr();
            }
        }

        private void ReadListFromQuickList(BinaryReader br, int encType)
        {
            var length = br.ReadLength();
            var totalSize = 0;
            Info info = new Info();
            info.Idle = _idle;
            info.Freq = _freq;
            info.Encoding = Constant.ObjEncoding.QUICKLIST;
            info.Zips = length;
            _callback.StartList(_key, _expiry, info);

            while (length > 0)
            {
                length--;

                if (encType == Constant.DataType.LIST_QUICKLIST_2)
                {
                    var container = br.ReadLength();

                    if (container != Constant.QuickListContainerFormats.PACKED
                        && container != Constant.QuickListContainerFormats.PLAIN)
                    {
                        throw new RDBParserException("Quicklist integrity check failed.");
                    }
                }

                var rawString = br.ReadStr();
                totalSize += rawString.Length;

                using (MemoryStream stream = new MemoryStream(rawString))
                {
                    var rd = new BinaryReader(stream);

                    if (encType == Constant.DataType.LIST_QUICKLIST_2)
                    {
                        // https://github.com/redis/redis/blob/7.0-rc3/src/listpack.c#L1284
                        // <total_bytes>
                        var bytes = lpGetTotalBytes(rd);
                        // <size>
                        var numEle = lpGetNumElements(rd);

                        info.Encoding = Constant.ObjEncoding.LISTPACK;

                        for (int i = 0; i < numEle; i++)
                        {
                            // <entry>
                            var entry = ReadListPackEntry(rd);
                            _callback.RPush(_key, entry.data);
                        }

                        var lpEnd = rd.ReadByte();
                        if (lpEnd != 0xFF) throw new RDBParserException($"Invalid list pack end - {lpEnd} for key {_key}");
                    }
                    else
                    {
                        var zlbytes = rd.ReadBytes(4);
                        var tailOffset = rd.ReadBytes(4);
                        var numEntries = rd.ReadUInt16();

                        for (int i = 0; i < numEntries; i++)
                        {
                            _callback.RPush(_key, ReadZipListEntry(rd));
                        }

                        var zlistEnd = rd.ReadByte();
                        if (zlistEnd != 255)
                        {
                            throw new RDBParserException("Invalid zip list end");
                        }
                    }
                }
            }

            info.SizeOfValue = totalSize;

            _callback.EndList(_key, info);
        }
    }
}
