# Persistência Oracle — referência da camada de dados

Consulte o **[README principal](../../README.md)** para configuração externa, preparação de banco novo, atualização do banco que já tem a migração inicial, indicadores e limites das evidências. O [guia técnico da API](../README.md) descreve a orquestração. Não execute EF e scripts indiscriminadamente sobre o mesmo histórico.

## Modelo e garantias

| Entidade / tabela | Garantias |
| --- | --- |
| `GuidanceRecord` / `GUIDANCE_RECORDS` | RequestId como PK; SessionId não único; UTC, Action, Result e ErrorCode. O fluxo atual grava somente sucessos, embora a entidade aceite metadados failure. |
| `SessionEvaluation` / `SESSION_EVALUATIONS` | Id como PK e SessionId com índice único; Outcome, Rating, NeededHumanHelp, Source e UTC. Coleta manual, sem texto livre. |

Não há tabela de usuários/sessões ou FK para SessionId de guidance, que não é único. Existência é validada no serviço de avaliação. Nenhum conteúdo de página, objetivo, instrução ou credencial é armazenado nessas tabelas.

UUIDs são strings canônicas `D` em VARCHAR2(36); timestamps usam TIMESTAMP(7) por convenção UTC, restaurando DateTimeKind.Utc na materialização. Avaliações usam NUMBER(2) para nota e NUMBER(1) para booleano numérico. Oracle **19c é o destino confirmado pela equipe**; runtime e design-time usam DatabaseVersion19. Essa informação não substitui teste real de tipos, privilégios ou consultas no esquema alvo.

## APIs de persistência

- `IGuidanceRecordStore.ExistsAsync` usa AnyAsync/AsNoTracking para requestId; AddAsync aguarda SaveChangesAsync.
- `ISessionEvaluationStore` consulta existência de sessão/avaliação e grava a entidade; a restrição única também protege a corrida de inserção.
- ORA-00001 é reconhecido pelo número do OracleException, não por comparação de mensagem, e convertido em DuplicateRecordException para tratamento no serviço.
- Falha/resultado não confirmado impede HTTP 200 de guidance e HTTP 201 de avaliação. Não há sucesso parcial.
- Cancelamento é propagado. Não há retry: uma gravação interrompida pode ter sido confirmada.
- Leituras administrativas usam AsNoTracking, agrupamento, filtros, contagem e paginação LINQ assíncronos, sem carregar a tabela inteira em memória.

## Configuração design-time

`GuidanceDbContextFactory` cria o contexto sem abrir conexão e lê, nesta ordem: appsettings.json opcional da pasta atual, User Secrets do assembly e variáveis de ambiente (maior prioridade, ConnectionStrings__Oracle). Execute os comandos EF na pasta `CitizenTech.Api`.

Sem conexão configurada, a factory registra o provedor sem host fictício: geração offline de migração/script é possível, mas aplicação ao banco exige conexão externa. **Não há EnsureCreated, migration automática ou seed no startup.**

## Artefatos existentes

Caminhos relativos à pasta `CitizenTech.Api`:

- `Data/Migrations/20261110134447_InitialGuidanceRecords.cs`, designer e snapshot.
- `Data/Migrations/20261111123553_AddSessionEvaluations.cs` e designer.
- `Data/Scripts/001_InitialGuidanceRecords.sql`: somente a migração inicial e histórico EF.
- `Data/Scripts/002_SessionEvaluations.sql`: delta inicial → avaliações, sem recriar GUIDANCE_RECORDS.
- `Data/Scripts/FindGuidanceRecord.sql`: bind `:request_id`.
- `Data/Scripts/FindSessionEvaluation.sql`: bind `:session_id` e conferência de duplicidades.

Não gere o script inicial usando um comando sem limites de migração: isso incluiria também as migrações posteriores. Os comandos com IDs explícitos estão no README principal. Escolha aplicação por EF **ou** scripts após revisão do histórico e autorização; não execute Down em banco compartilhado.

## Evidências e limites

A existência desses artefatos não permite concluir se já foram aplicados no banco externo. Nenhuma aplicação, conexão real Oracle ou alteração de dados foi realizada nesta revisão documental. Testes de consultas usam SQLite em memória; doubles simulam gravação e conflito. Ainda é preciso comprovar no Oracle a tradução/execução LINQ, materialização UTC/tipos, restrições, concorrência ORA-00001 e indisponibilidade real.

Versões diretas e resultado atual da suíte estão centralizados no README principal para evitar duplicação e divergência. O manifesto de dotnet-ef fica em `CitizenTech.Api/dotnet-tools.json`.
