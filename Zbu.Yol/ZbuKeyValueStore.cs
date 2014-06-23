using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace Zbu.Yol
{
    // implements ZpqrtBnk key-value db store
    // there should be a key-value db store in Umbraco
    class ZbuKeyValueStore
    {
        public static string GetValue(string key)
        {
            var db = ApplicationContext.Current.DatabaseContext.Database;
            var kv = db.SingleOrDefault<ZbuKeyValue>("SELECT * FROM ZbuKeyValue WHERE KVKey=@0", key);
            return kv == null ? null : kv.Value;
        }

        public static void SetValue(string key, string value)
        {
            SetValue(key, null, false, value);
        }

        public static void SetValue(string key, string expected, string value)
        {
            SetValue(key, expected, true, value);
        }

        private static void SetValue(string key, string expected, bool checkExpected, string value)
        {
            var db = ApplicationContext.Current.DatabaseContext.Database;

            IDbTransaction trx = null;
            try
            {
                // BeginTransaction does a simple IDbTransaction.BeginTransaction() with default isolation level
                // we want RepeatableRead so that noone can change the value while we're verifying it
                // so we're entirely bypassing Umbraco's transaction management which prob. is a bad thing
                //db.BeginTransaction();
                trx = db.Connection.BeginTransaction(IsolationLevel.RepeatableRead);

                var kv = db.SingleOrDefault<ZbuKeyValue>("SELECT * FROM ZbuKeyValue WHERE KVKey=@0", key);
                var ok = !checkExpected || (string.IsNullOrWhiteSpace(expected)
                    ? kv == null || string.IsNullOrWhiteSpace(kv.Value)
                    : kv != null && kv.Value == expected);
                if (!ok)
                    throw new Exception("Could not save state because state has changed in database.");
                var nkv = new ZbuKeyValue { Key = key, Value = value };
                if (kv == null)
                    db.Insert(nkv);
                else
                    db.Update(nkv);
                
                //db.CompleteTransaction();
                trx.Commit();
            }
            catch
            {
                //db.AbortTransaction();
                if (trx != null)
                    trx.Rollback();
                throw;
            }
        }

        public static void EnsureInstalled(ApplicationContext applicationContext)
        {
            var db = applicationContext.DatabaseContext.Database;

            // create if not exists
            if (!db.TableExist("ZbuKeyValue"))
                db.CreateTable<ZbuKeyValue>(false);            
        }

        [TableName("ZbuKeyValue")]
        [PrimaryKey("KVKey")]
        [ExplicitColumns]
        public class ZbuKeyValue
        {
            [Column("KVKey")]
            [PrimaryKeyColumn]
            public string Key { get; set; }

            [Column("KVValue")]
            [SpecialDbType(SpecialDbTypes.NTEXT)]
            public string Value { get; set; }
        }
    }
}
