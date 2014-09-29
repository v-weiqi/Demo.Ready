sqlcmd -S .\SQLExpress -i TrackDB.1.CreateDatabase.sql
sqlcmd -S .\SQLExpress -i TrackDB.2.Schema.sql
sqlcmd -S .\SQLExpress -i WorkflowDB.1.StoreSchema.sql
sqlcmd -S .\SQLExpress -i WorkflowDB.2.StoreLogic.sql