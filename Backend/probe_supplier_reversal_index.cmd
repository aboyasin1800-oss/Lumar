@echo off
sqlcmd -S "YASIN-YASIN\SQLEXPRESS" -d LUMAR_ERP_TEST -Q "SELECT name, filter_definition, is_unique FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.SupplierFinancialPayments') AND name LIKE '%OriginalReversal%';" > "D:\YASIN\Backend\probe_supplier_reversal_index.txt" 2>&1
