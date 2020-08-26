using System;
using Miyabi.Binary.Models;
using Miyabi.ContractSdk;
using Miyabi.ModelSdk.Execution;

namespace Contract.Sample
{
    public class SC1 : ContractBase
    {
        static string s_tableName = "MyTable";

        public SC1(ContractInitializationContext ctx)
            : base(ctx)
        { }

        public override bool Instantiate(string[] args)
        {
            if (args.Length < 1)
            {
                return false;
            }

            var tableName = s_tableName + InstanceName;
            var tableDescriptor = new BinaryTableDescriptor(tableName, false);

            try
            {
                StateWriter.AddTable(tableDescriptor);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Delete()
        {
            var tableName = s_tableName + InstanceName;
            try
            {
                StateWriter.DeleteTable(tableName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Write(Miyabi.ByteString key, Miyabi.ByteString val)
        {
            var tableName = s_tableName + InstanceName;
            var ctx = StateWriter;
            bool res = ctx.TryGetTableWriter(
                tableName, out IBinaryTableWriter table);
            if (!res)
            {
                throw new InvalidOperationException("missing table");
            }

            table.SetValue(key, val);
        }

        public Miyabi.ByteString Read(Miyabi.ByteString key)
        {
            var tableName = s_tableName + InstanceName;
            IStateReader reader = StateWriter;
            if (!reader.TryGetTableReader(
                tableName, out IBinaryTableReader table))
            {
                return null;
            }

            table.TryGetValue(key, out var value);
            return value;
        }
    }
}
