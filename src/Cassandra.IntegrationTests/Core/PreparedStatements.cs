﻿//
//      Copyright (C) 2012 DataStax Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if MYTEST

#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif


namespace Cassandra.IntegrationTests.Core
{

    [TestClass]
    public partial class PreparedStatementsTests
    {
        Session Session;

        [TestInitialize]
        public void SetFixture()
        {
            CCMBridge.ReusableCCMCluster.Setup(2);
            CCMBridge.ReusableCCMCluster.Build(Cluster.Builder());
            Session = CCMBridge.ReusableCCMCluster.Connect("tester");
        }

        [TestCleanup]
        public void Dispose()
        {
            CCMBridge.ReusableCCMCluster.Drop();
        }
        
        public PreparedStatementsTests()
        {
        }

        public void insertingSingleValuePrepared(Type tp)
        {
            string cassandraDataTypeName = QueryTools.convertTypeNameToCassandraEquivalent(tp);
            string tableName = "table" + Guid.NewGuid().ToString("N");

            Session.WaitForSchemaAgreement(
                QueryTools.ExecuteSyncNonQuery(Session, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         value {1}
         );", tableName, cassandraDataTypeName))
            );

            List<object[]> toInsert = new List<object[]>(1);
            var val = Randomm.RandomVal(tp);
            if (tp == typeof(string))
                val = "'" + val.ToString().Replace("'", "''") + "'";

            var row1 = new object[2] { Guid.NewGuid(), val };

            toInsert.Add(row1);

            var prep = QueryTools.PrepareQuery(this.Session, string.Format("INSERT INTO {0}(tweet_id, value) VALUES ({1}, ?);", tableName, toInsert[0][0].ToString()));
            QueryTools.ExecutePreparedQuery(this.Session, prep, new object[1] { toInsert[0][1] });

            QueryTools.ExecuteSyncQuery(Session, string.Format("SELECT * FROM {0};", tableName), ConsistencyLevel.One, toInsert);
            QueryTools.ExecuteSyncNonQuery(Session, string.Format("DROP TABLE {0};", tableName));
        }

        public void preparedSelectTest()
        {
            string tableName = "table" + Guid.NewGuid().ToString("N");

            try
            {
                Session.WaitForSchemaAgreement(
                    QueryTools.ExecuteSyncNonQuery(Session, string.Format(@"CREATE TABLE {0}(
         tweet_id int PRIMARY KEY,
         numb double,
         label text
         );", tableName))
                );
            }
            catch (AlreadyExistsException)
            {
            }

            for (int i = 0; i < 10; i++)
                Session.Execute(string.Format("INSERT INTO {0}(tweet_id, numb, label) VALUES({1},{2},'{3}')", tableName, i, i * .1d, "row" + i.ToString()));

            var prep_select = QueryTools.PrepareQuery(Session, string.Format("SELECT * FROM {0} WHERE tweet_id = ?;", tableName));
            
            int rowID = 5;
            var result = QueryTools.ExecutePreparedSelectQuery(this.Session, prep_select, new object[1] { rowID });
            using (result)
            {
                foreach (var row in result.GetRows())
                    Assert.True((string)row.GetValue(typeof(int), "label") == "row" + rowID.ToString());
            }
            Assert.True(result.Columns != null);
            Assert.True(result.Columns.Length == 3);
            QueryTools.ExecuteSyncNonQuery(Session, string.Format("DROP TABLE {0};", tableName));
        }

        public void massivePreparedStatementTest()
        {
            string tableName = "table" + Guid.NewGuid().ToString("N");

            try
            {
                Session.WaitForSchemaAgreement(
                    QueryTools.ExecuteSyncNonQuery(Session, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         numb1 double,
         numb2 int
         );", tableName))
                );
            }
            catch (AlreadyExistsException)
            {
            }
            int numberOfPrepares = 100;

            List<object[]> values = new List<object[]>(numberOfPrepares);
            List<PreparedStatement> prepares = new List<PreparedStatement>();

            Parallel.For(0, numberOfPrepares, i =>
            {

                var prep = QueryTools.PrepareQuery(Session, string.Format("INSERT INTO {0}(tweet_id, numb1, numb2) VALUES ({1}, ?, ?);", tableName, Guid.NewGuid()));

                lock (prepares)
                    prepares.Add(prep);

            });

            Parallel.ForEach(prepares, prep =>
            {
                QueryTools.ExecutePreparedQuery(this.Session, prep, new object[] { (double)Randomm.RandomVal(typeof(double)), (int)Randomm.RandomVal(typeof(int)) });
            });

            QueryTools.ExecuteSyncQuery(Session, string.Format("SELECT * FROM {0};", tableName), Session.Cluster.Configuration.QueryOptions.GetConsistencyLevel());
        }
    }
}
