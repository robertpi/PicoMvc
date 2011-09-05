using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Raven.Abstractions.Indexing;
using Raven.Client.Indexes;
using Common;

namespace WebHost.Indexes
{
    public class Communes_Search : AbstractIndexCreationTask
    {
        public override IndexDefinition CreateIndexDefinition()
        {
            return new IndexDefinitionBuilder<Commune>
            {
                Map = communes => from commune in communes select new { commune.Name, commune.Postcode }
            }.ToIndexDefinition(this.Conventions);
        }
    }
}