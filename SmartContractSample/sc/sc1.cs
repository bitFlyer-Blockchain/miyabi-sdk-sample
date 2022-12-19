using System;
using Miyabi.Binary.Models;
using Miyabi.Contract.Models;
using Miyabi.ContractSdk;
using Miyabi.ModelSdk.Execution;

namespace Contract.Sample
{
    public class SC1 : ContractBase
    {
        static string s_tableName = "MyTable";

        public SC1(ContractInitializationContext ctx)
            : base(ctx)
        {
        }

        public override bool Instantiate(string[] args)
        {
            if (args.Length < 1)
            {
                return false;
            }

            string tableName = s_tableName + InstanceName;
            var address = new[] {ContractAddress.FromInstanceId(InstanceId)};
            BinaryTableDescriptor tableDescriptor = new BinaryTableDescriptor(
                tableName,
                false,
                false,
                address);

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
            string tableName = s_tableName + InstanceName;
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
            string tableName = s_tableName + InstanceName;
            IContractStateWriter ctx = StateWriter;
            bool res = ctx.TryGetTableWriter(
                tableName, out IBinaryTableWriter table);
            if (!res)
            {
                throw new InvalidOperationException("missing table");
            }

            table.UpsertRow(key, val);
        }

        public Miyabi.ByteString Read(Miyabi.ByteString key)
        {
            string tableName = s_tableName + InstanceName;
            IStateReader reader = StateWriter;
            if (!reader.TryGetTableReader(
                tableName, out IBinaryTableReader table))
            {
                return null;
            }

            table.TryGetData(key, out Miyabi.ByteString value);
            return value;
        }
    }
}
