# Persistência Oracle

Somente metadados técnicos em `GUIDANCE_RECORDS`; nenhum conteúdo de requisição, página ou orientação é persistido/logado nesta camada. `REQUEST_ID` é chave primária, protegendo também contra concorrência. UUIDs são strings canônicas `D`, minúsculas, em `VARCHAR2(36)`. `CREATED_AT_UTC` usa `TIMESTAMP(7)` com convenção UTC; o materializador restaura `DateTimeKind.Utc`.

## APIs de integração

- `GuidanceRecord.Success(Guid requestId, Guid sessionId, string action)` define horário UTC no servidor.
- Construtor público para metadados de falha: `GuidanceRecord(Guid, Guid, DateTime createdAtUtc, string? action, string result, string? errorCode)`.
- `IGuidanceRecordStore.ExistsAsync(Guid requestId, CancellationToken)` verifica existência via `AnyAsync`, sem carregar registros.
- `IGuidanceRecordStore.AddAsync(GuidanceRecord, CancellationToken)` salva via `SaveChangesAsync`.
- Implementação: `OracleGuidanceRecordStore(GuidanceDbContext)`; registrar contexto e store com escopo por requisição.
- `PersistenceException`: falha de banco/timeout sem confirmação de persistência; `DuplicateRecordException` deriva dela e identifica especificamente ORA-00001. Capturar duplicidade primeiro no serviço. As mensagens externas devem ser sanitizadas; não expor/logar `InnerException` indiscriminadamente.
- `OperationCanceledException` propaga inalterada: cabe ao serviço distinguir desconexão do cliente de timeout de persistência e traduzir este último para 503. Nenhum retry nesta camada; não habilitar estratégia de retry na configuração do contexto, pois uma escrita interrompida pode ter sido confirmada.

## Configuração e compatibilidade

A factory `GuidanceDbContextFactory` é apenas design-time e não abre conexão ao criar o contexto. Ela lê `ConnectionStrings:Oracle`, nesta precedência: `appsettings.json` opcional da pasta atual, user secrets do assembly e variáveis de ambiente (maior prioridade; chave `ConnectionStrings__Oracle`). Execute os comandos na pasta do projeto. Configure segredos fora do repositório. Sem conexão configurada, registra somente o provedor Oracle, sem placeholder: geração offline funciona, mas `database update` falha sem tentar DNS/conexão fictícia. Para aplicação explícita e revisada, configure a conexão externamente ou passe `dotnet ef database update --context GuidanceDbContext --connection <conexão-externa>` (atenção ao histórico do shell). O runtime precisa fornecer a conexão por configuração externa. Configuração esperada:

```csharp
options.UseOracle(connection, oracle =>
    oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
```

**DatabaseVersion19 é provisório. Confirmar versão real, suporte pelo provedor, privilégios, tipos e compatibilidade do Oracle da faculdade antes de aplicar qualquer migration/script.** Caso seja necessário mudar a compatibilidade, atualizar factory e configuração runtime juntas e regenerar/revisar SQL. Não há DDL nem migration automática no startup nesta camada.

## Artefatos offline (não aplicados)

- Migration `Data/Migrations/20261110134447_InitialGuidanceRecords.cs`, designer e snapshot gerados pelo EF.
- `Data/Scripts/001_InitialGuidanceRecords.sql`: script completo da migration inicial, incluindo histórico EF. Não é um script para reaplicação indiscriminada em banco existente.
- `Data/Scripts/FindGuidanceRecord.sql`: consulta parametrizada por `:request_id` para evidência sanitizada; não criar endpoint de consulta. Um registro `success` comprova orientação registrada, não tarefa concluída.

Comandos a partir da pasta do projeto:

```powershell
dotnet restore
dotnet tool restore
dotnet build
dotnet ef migrations script --context GuidanceDbContext --output Data/Scripts/001_InitialGuidanceRecords.sql
dotnet ef migrations has-pending-model-changes --context GuidanceDbContext
```

Nenhum comando de aplicação (`database update`) foi executado. Nenhuma conexão real ou integração Oracle foi testada. Revisar SQL com o responsável pelo banco e aplicar separadamente com credenciais fora do repositório, após confirmar compatibilidade.

## Versões

- .NET target: `net10.0`; SDK utilizado: `10.0.403`.
- `Oracle.EntityFrameworkCore`: `10.23.26400` (asset net10; EF Relational >=10, <11).
- `Oracle.ManagedDataAccess.Core`: `23.26.400` transitivo.
- `Microsoft.EntityFrameworkCore.Design` e EF Core resolvido: `10.0.14`.
- `dotnet-ef`: `10.0.14`, manifesto local `dotnet-tools.json`.
- `System.Security.Cryptography.Pkcs`: referência direta `10.0.14` para evitar a versão transitiva Oracle `8.0.1`, sinalizada com NU1902/NU1903 durante o restore inicial.
