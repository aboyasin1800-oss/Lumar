# LUMAR Rebuild Project Structure

This workspace is a clean rebuild foundation. It contains no recovered application source code.

```text
D:\YASIN
|- backend\LUMAR_ERP_API_V2
|  |- Authorization
|  |- Configuration
|  |- Controllers
|  |- Data
|  |- DTOs
|  |- Migrations
|  |- Middleware
|  |- Models
|  |- Properties
|  |- Repositories
|  |- Services
|  `- Tests
|- frontend\tailoring_system
|  |- lib\core
|  |- lib\models
|  |- lib\services
|  |- lib\repositories
|  |- lib\providers
|  |- lib\screens
|  |- lib\widgets
|  |- lib\mobile
|  |- assets
|  |- test
|  |- android
|  |- windows
|  `- web
|- database
|- docs
|- scripts
|- recovery-evidence
`- backups
```

The intended future database target is SQL Server `YASIN-YASIN\SQLEXPRESS`, database `LUMAR_ERP`. No connection string is stored in this repository, and this task does not change that database or create migrations.