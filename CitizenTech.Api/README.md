# CitizenTech API — Oracle e Gemini

Backend do assistente para idosos e pessoas com dificuldade em tecnologia. O app Android lê elementos da tela no Chrome e recebe uma orientação e, quando aplicável, o ID real do elemento a destacar. A API não toca, não preenche e não confirma agendamentos pelo cidadão.

**Estado atual:** orientação por **Fake ou Gemini**, selecionada explicitamente em `Guidance:Provider`; persistência **Oracle real e obrigatória** em ambos os modos. A expansão administrativa acrescenta avaliação manual de sessão, consultas e métricas protegidas por chave administrativa; não implementa autenticação de cidadãos. Não há publicação Azure nem app neste projeto. A especificação original `Especificacao_App_CitizenTech_MVP.md`, revisão 1.1, permanece inalterada; a expansão está documentada separadamente abaixo e não muda o contrato de guidance. Chamadas reais ao Google e aplicação de migrações dependem de execução manual autorizada.

## Estrutura

```text
CitizenTech.slnx                         (na pasta superior, solução existente)
CitizenTech.Api/
  Controllers/GuidanceController.cs      POST guidance e GET health
  Contracts/                            requisição, respostas e validação
  Models/GuidanceRecord.cs               metadados e invariantes encapsulados
  Data/                                 DbContext, store Oracle, factory e migração
    Migrations/                         migrações EF e snapshot
    Scripts/                            scripts revisáveis e consultas por bind
  Services/                             serviço, interface, provedores Fake/Gemini e configuração
  Errors/                               envelope, limites e documentação OpenAPI
  CitizenTech.Api.http                   exemplos manuais
CitizenTech.Api.Tests/                   projeto separado, na mesma solução
```

## Pré-requisitos e execução

Preservado o projeto do Visual Studio em **.NET 10**. SDKs observados: 10.0.400 e 10.0.403. Pacotes estão fixados em `CitizenTech.Api.csproj`, incluindo provedor oficial `Oracle.EntityFrameworkCore` 10.23.26400 e EF Design 10.0.14. Dependência criptográfica fixada em versão corrigida para evitar alertas transitivos de segurança.

Na pasta da solução:

```powershell
dotnet restore CitizenTech.slnx
dotnet build CitizenTech.slnx
dotnet test CitizenTech.slnx
```

Na pasta `CitizenTech.Api`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile --urls http://localhost:5075
```

Em desenvolvimento: Swagger em `http://localhost:5075/swagger`, OpenAPI em `/openapi/v1.json`. O `.http` utiliza essa porta explícita; alternativamente use os perfis do Visual Studio e ajuste a URL. HTTP somente para desenvolvimento; demonstração hospedada deverá usar HTTPS. No celular físico use IP local, escuta na interface e firewall adequados, não `localhost`. CORS não corrige problemas de rede no app nativo.

O health responde `{"status":"ok","contractVersion":"1"}` sem banco/IA. Sem configuração Oracle, requisição válida retorna **503**, não 200. Gemini sem chave/modelo válido retorna 502/AI_UNAVAILABLE ao atingir a geração, sem impedir o health. Seleção de provedor ausente/inválida também não ativa Fake e resulta em 502. A consulta Oracle continua anterior à geração; uma falha nela resulta em 503 antes de chamar o provedor.

## Gemini: configuração e teste manual

Configurações finais (nomes com `__` são variáveis de ambiente):

| Configuração | Variável | Uso |
| --- | --- | --- |
| `Guidance:Provider` | `Guidance__Provider` | `Fake` ou `Gemini`, seleção explícita |
| `Gemini:ApiKey` | `Gemini__ApiKey` | Chave secreta do Google, somente no servidor |
| `Gemini:Model` | `Gemini__Model` | Identificador oficial do modelo |

Modelo escolhido: **`gemini-3.5-flash-lite`**, listado como **Stable** e com suporte a saída estruturada na documentação oficial consultada em 10/11/2026. Modelo configurável; verificar disponibilidade, preços e suporte a `generateContent`/JSON schema antes de trocar. Não usamos modelos preview nem alias `latest`.

Referências oficiais consultadas:
- [Modelo Gemini 3.5 Flash-Lite](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite)
- [generateContent e formato das respostas](https://ai.google.dev/api/generate-content)
- [Saída estruturada](https://ai.google.dev/gemini-api/docs/structured-output)
- [Preços](https://ai.google.dev/gemini-api/docs/pricing) e [limites de uso](https://ai.google.dev/gemini-api/docs/rate-limits)

Na pasta da API, em uma sessão PowerShell local, substitua os placeholders **somente na sua máquina**. Não compartilhe histórico, prints ou saída contendo segredos:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Guidance__Provider = 'Gemini'
$env:Gemini__Model = 'gemini-3.5-flash-lite'
$env:Gemini__ApiKey = '<SUA_CHAVE_GEMINI>'
# Preserve a conexão Oracle já configurada via User Secrets ou ambiente.
dotnet run --no-launch-profile --urls http://localhost:5075
```

Alternativa persistente para desenvolvimento: **Gerenciar Segredos do Usuário** no Visual Studio (`Gemini:ApiKey`). Não gravar chave no appsettings/repositório. Variáveis de ambiente sobrepõem User Secrets; remova valores antigos ao trocar de configuração.

Com a API iniciada, use o exemplo de `CitizenTech.Api.http` ou Swagger, com IDs novos e dados fictícios. **Esse POST faz uma chamada real ao Google e pode consumir quota/cobrança.** Depois de 200, confira os metadados no Oracle como descrito abaixo. Sem evidência dessa execução, testes simulados não comprovam integração real com Google. Não envie dados pessoais nem valores de formulários; revise também os termos de tratamento de dados do plano Google escolhido.

Para voltar explicitamente à simulação, antes de reiniciar:

```powershell
$env:Guidance__Provider = 'Fake'
Remove-Item Env:Gemini__ApiKey -ErrorAction SilentlyContinue
```

**Fake** é determinístico, não chama Google e continua exigindo Oracle no POST. **Gemini** faz somente um `POST /v1beta/models/{model}:generateContent`, chave no header `x-goog-api-key`, saída `application/json` com `responseJsonSchema`, sem ferramentas, retries, redirects ou fallback. Recebe apenas objetivo, mensagem, histórico e elementos; não recebe UUIDs de correlação ou configuração interna. Texto recebido é isolado em JSON de usuário e tratado como dado não confiável nas instruções de sistema. A validação de ações, instrução e alvo no backend permanece obrigatória; schema e prompt não garantem correção semântica absoluta.

HTTP Google não exitoso (inclusive 429/quota), bloqueio, término diferente de `STOP`, corpo vazio, truncado ou inválido resultam em **502/AI_UNAVAILABLE**, nunca no 429 local da API. Timeout da geração resulta em **504/AI_TIMEOUT**; cancelamento do cliente é propagado, sem fallback e sem prometer cancelamento de cobrança. A geração usa no máximo 15 s e é reduzida pelo tempo já consumido para reservar 5 s à persistência dentro dos 20 s totais. Não habilitar logs de corpos, headers ou diagnóstico sensível; o client Gemini tem seus loggers removidos e header secreto redigido.

Os testes `GeminiGuidanceProviderTests` e `GuidanceRegistrationTests` simulam HTTP/configuração e preservam os testes de persistência anteriores. `dotnet test` não consome Gemini. A migração já aplicada **não precisa ser reaplicada** para habilitar este provedor.

## Conexão Oracle sem versionar senha

Mantenha a conexão e a versão Oracle já validadas na etapa anterior. Em um ambiente novo, configure localmente com **User Secrets**, nunca em arquivos versionados. O projeto já tem `UserSecretsId`. No terminal local (substitua os marcadores apenas na sua máquina e evite gravar o comando com senha em histórico compartilhado):

```powershell
dotnet user-secrets set 'ConnectionStrings:Oracle' 'User Id=<USUARIO>;Password=<SENHA>;Data Source=<HOST>:<PORTA>/<SERVICO>;Connection Timeout=5;' --project CitizenTech.Api.csproj
```

Também é possível usar **Gerenciar Segredos do Usuário** no Visual Studio. Em ambiente hospedado, configurar `ConnectionStrings__Oracle` como variável de ambiente/segredo. Não compartilhar saída de secrets, strings, senhas ou prints de conexão. A chave Gemini também deve permanecer em User Secrets ou variável de ambiente, conforme a seção abaixo.

## Migração: revisar antes de aplicar

**Nada aplica DDL na inicialização.** Não há `EnsureCreated`. A factory de design-time permite gerar artefatos sem conexão com banco real e lê User Secrets/variáveis de ambiente para aplicação explícita. Sem configuração não utiliza host fictício e não permite atualizar o banco. Os artefatos usam compatibilidade **Oracle 19** de forma provisória e tipos explícitos `VARCHAR2` e `TIMESTAMP`, sem depender de BOOLEAN SQL. Isso não afirma que a faculdade usa Oracle 19c.

1. Confirmar versão do banco com a faculdade (por exemplo, consultar `SELECT banner FROM v$version` se autorizado), esquema, privilégios e existência de `GUIDANCE_RECORDS`/tabela de histórico EF.
2. Conferir compatibilidade no `Program.cs` e `Data/GuidanceDbContextFactory.cs`. Se diferente da compatibilidade assumida, ajustar ambos e regenerar/revisar os artefatos antes de qualquer aplicação.
3. Revisar `Data/Scripts/001_InitialGuidanceRecords.sql` e a migração inicial em `Data/Migrations`. Eles criam somente a tabela necessária e o histórico EF. **Se existir tabela com mesmo nome, parar e reconciliar com o responsável pelo esquema; não apagar/recriar tabelas da faculdade.** Não executar o método Down no banco compartilhado.
4. Restaurar a ferramenta local e gerar script para revisão, na pasta da API:

```powershell
dotnet tool restore
dotnet ef migrations script 0 20261110134447_InitialGuidanceRecords --output Data/Migrations/initial.sql
```

5. Somente após revisão e autorização, com a versão validada e conexão configurada, aplicar explicitamente:

```powershell
dotnet ef database update
```

Verifique as instruções da factory: comandos design-time usam conexão de ambiente/User Secrets quando configurada; sem conexão, a aplicação da migração falha sem acessar host fictício. O script gerado inicialmente está em `Data/Scripts/001_InitialGuidanceRecords.sql` e a consulta por `REQUEST_ID` em `Data/Scripts/FindGuidanceRecord.sql`. Use o UUID textual do POST como bind, sem divulgar dados de conexão.

### Migração gerada para avaliações (delta)

A migração **`20261111123553_AddSessionEvaluations`** já está gerada em `Data/Migrations`. O script existente `Data/Scripts/002_SessionEvaluations.sql` foi gerado **somente com o delta desde `20261110134447_InitialGuidanceRecords`**: cria `SESSION_EVALUATIONS`, o índice único por sessão e a entrada no histórico EF, sem recriar a base inicial. Preserve a migração inicial e os registros existentes; não crie outra migração para esta expansão.

1. Revise a migração existente, o snapshot e o script. Para compatibilidade **Oracle 19**, `RATING` usa `NUMBER(2)` e `NEEDED_HUMAN_HELP` usa `NUMBER(1)` (booleano representado numericamente, sem BOOLEAN SQL); UUIDs usam `VARCHAR2(36)` e a data `TIMESTAMP(7)`.
2. Se precisar reproduzir o script para revisão, use os IDs concretos, na pasta da API:

```powershell
dotnet tool restore
dotnet ef migrations script 20261110134447_InitialGuidanceRecords 20261111123553_AddSessionEvaluations --output Data/Scripts/002_SessionEvaluations.sql
```

3. Confirme compatibilidade Oracle, histórico EF e permissões com o responsável. Não aplique sobre objetos conflitantes nem execute Down em banco compartilhado. Após revisão e autorização, com conexão local configurada, aplique **manualmente** a migração existente:

```powershell
dotnet ef database update 20261111123553_AddSessionEvaluations
```

A existência dos artefatos gerados não comprova aplicação no Oracle. Estes comandos são instruções de revisão/aplicação manual, não relato de execução nesta atualização documental; evidências anteriores permanecem com seu escopo original. Para conferência manual, use `Data/Scripts/FindSessionEvaluation.sql` com o UUID da sessão como bind e `Data/Scripts/FindGuidanceRecord.sql` com o requestId; nunca inclua conexão ou senha na evidência.

## Expansão administrativa (separada da especificação original)

Os **quatro endpoints** abaixo exigem um único header `X-Admin-Key`, inclusive o POST fora do prefixo `/admin`. Configure `Admin:ApiKey` **somente por ambiente local**, antes de iniciar a API:

```powershell
$env:Admin__ApiKey = 'CHAVE_APENAS_NO_AMBIENTE_LOCAL'
dotnet run --no-launch-profile --urls http://localhost:5075
```

O valor acima é um marcador para substituição local, não uma credencial a reutilizar. Não coloque o segredo em appsettings, `.http`, commits, prints ou vídeos. O cliente do `.http` precisa herdar a variável de ambiente (inicie/reinicie-o nesse ambiente); os headers usam `{{$processEnv Admin__ApiKey}}`, sem chave literal. Use HTTPS fora do desenvolvimento local. A chave é proteção administrativa simples, não login, identidade por usuário ou autorização por perfis.

### Contratos e exemplos completos

`CitizenTech.Api.http` contém requisições executáveis para as quatro operações com todos os campos/filtros, usando a mesma sessão do guidance:

| Operação | Entrada | Resposta |
| --- | --- | --- |
| `POST /api/v1/sessions/{sessionId}/evaluation` | UUID não vazio; JSON `{"outcome":"completed","rating":5,"neededHumanHelp":false}` | 201 com `id`, `sessionId`, `outcome`, `rating`, `neededHumanHelp`, `source:"manual"`, `createdAtUtc` gerados/persistidos pelo servidor |
| `GET /api/v1/admin/sessions?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z&page=1&pageSize=20&hasFailures=false&hasEvaluation=true&outcome=completed` | Intervalo e filtros opcionais | 200 com `contractVersion`, `from`, `to`, `page`, `pageSize`, `totalItems`, `totalPages`, `items` |
| `GET /api/v1/admin/sessions/{sessionId}?page=1&pageSize=20` | UUID não vazio; paginação da timeline | 200 com `contractVersion`, `session`, `evaluation` separado (objeto ou null) e `timeline:{page,pageSize,totalItems,totalPages,items}` |
| `GET /api/v1/admin/metrics?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z` | Intervalo opcional | 200 com contagens, taxas, média e `errorCodes` descritos abaixo |

Avaliação requer sessão com ao menos um guidance persistido; é única por sessão e não há edição/exclusão. `outcome` aceita exatamente `completed`, `partially_completed` ou `not_completed`; `rating` é inteiro de 1 a 5 e `neededHumanHelp` é booleano obrigatório. Não envie texto livre, `source`, IDs ou data no corpo: propriedades desconhecidas são rejeitadas. A avaliação é **manual**, não inferida pela IA.

**Intervalos:** envie `from` e `to` juntos ou omita ambos. O padrão é `[agora UTC - 30 dias, agora UTC)`. Datas exigem horário e timezone explícito (`Z` ou offset `±HH:mm`), são normalizadas para UTC, com `from < to`; `from` é inclusivo e `to` exclusivo. Codifique `+` como `%2B` na query ao usar offset positivo. Um único limite, data sem timezone ou intervalo invertido retorna 400.

**Lista:** seleciona sessões com atividade técnica no intervalo, mas o resumo e os filtros usam o **histórico total da sessão**, não só a janela. Cada item contém `sessionId`, `firstRecordedAtUtc`, `lastRecordedAtUtc`, `totalGuidanceRecords`, `successfulRecords`, `failedRecords`, `hasEvaluation`, `outcome` e `rating` (estes dois últimos são null quando não há avaliação). `hasFailures` é filtro, não campo do DTO. `hasFailures` e `hasEvaluation` aceitam `true`/`false`; omitidos não filtram. `outcome` restringe a avaliação e é incompatível com `hasEvaluation=false` (400). A avaliação associada não precisa ter sido criada dentro do intervalo. Ordenação: última atividade decrescente, desempate por sessionId. `totalItems` conta sessões após filtros, antes da paginação.

**Detalhe:** não recebe intervalo; `session` usa o mesmo resumo da lista, e `evaluation` é um campo separado, com `id`, `sessionId`, `outcome`, `rating`, `neededHumanHelp`, `source`, `createdAtUtc`, ou null. Resumo e avaliação são totais da sessão. A timeline contém somente metadados `requestId`, `createdAtUtc`, `action`, `result`, `errorCode`, ordenados por data crescente e requestId. Não expõe conteúdo de tela, objetivo, prompt ou instrução. Paginação em lista e timeline: `page=1`, `pageSize=20` por padrão; `page >= 1`, tamanho de 1 a 100 e deslocamento dentro de Int32. Lista e timeline incluem `totalPages = ceil(totalItems / pageSize)` (zero sem itens). Página além do fim retorna itens vazios, mantendo os totais.

### Métricas: populações e fórmulas

Há **duas populações distintas**, sem conversão entre elas:

A resposta inclui `contractVersion`, `from` e `to`, além dos campos abaixo:

- Técnica: registros de guidance com `createdAtUtc` no intervalo. `totalSessions` é o número de sessionIds distintos nesses registros; `totalGuidanceRecords`, `successfulRecords` e `failedRecords` contam registros totais, `success` e `failure`. `errorCodes` agrupa códigos não nulos apenas de registros `failure`.
- Avaliações: avaliações **criadas** no intervalo, mesmo que a atividade técnica da sessão esteja fora dele. `totalEvaluations`, `completedEvaluations`, `partiallyCompletedEvaluations`, `notCompletedEvaluations` e `neededHumanHelpEvaluations` contam essa população. Não há taxa de conversão entre registros/sessões e avaliações, nem exigência de correspondência temporal das duas populações.

| Campo | Fórmula |
| --- | --- |
| `technicalSuccessRate` | `100 * successfulRecords / totalGuidanceRecords` |
| `completionRate` | `100 * completedEvaluations / totalEvaluations` |
| `humanHelpRate` | `100 * neededHumanHelpEvaluations / totalEvaluations` |
| `averageRating` | soma das notas / `totalEvaluations` |

Percentuais estão na escala **0–100**, não 0–1. Taxas e média são `null` quando o denominador é zero; contagens ficam zero e `errorCodes` vazio. Arredondamento a duas casas, ponto médio afastado de zero. `errorCodes` usa `{value,count}`, ordenado por valor, sem preencher categorias ausentes com zero.

**Limitações:** falhas de geração/persistência não são gravadas pelo fluxo atual; assim, `failedRecords`, o filtro `hasFailures` e `errorCodes` não medem todas as falhas ocorridas, e `technicalSuccessRate` reflete somente registros persistidos, não todas as tentativas. `success` é orientação válida persistida e `action=complete` **não prova conclusão real da tarefa**. Conclusão e ajuda humana dependem da avaliação manual, sujeita a seleção e relato; não são garantia de eficácia nem conclusão inferida pela IA.

### Erros administrativos e correlação

Mantém-se o envelope `{contractVersion,requestId,error:{code,message}}`. Nas quatro rotas, `requestId` de erro é um **UUID gerado pelo servidor** para correlação, não o sessionId nem um requestId fornecido pelo cliente. Não envie corpo de guidance para correlação nesses endpoints.

| Status / código | Situação |
| --- | --- |
| 400 `INVALID_REQUEST` | UUID, corpo, filtro, intervalo ou paginação inválidos |
| 401 `UNAUTHORIZED` | Chave ausente, incorreta ou header múltiplo/com vírgula |
| 503 `ADMIN_NOT_CONFIGURED` | `Admin:ApiKey` ausente ou em branco; falha fechada |
| 404 `SESSION_NOT_FOUND` | Sessão sem registro técnico, no detalhe ou avaliação |
| 409 `SESSION_ALREADY_EVALUATED` | Avaliação já existente, inclusive disputa concorrente |
| 503 `PERSISTENCE_UNAVAILABLE` | Falha de leitura/gravação Oracle |
| 429 `RATE_LIMITED` | Cota administrativa compartilhada entre os quatro novos endpoints; tentativas com chave inválida contam, sem consumir a cota de guidance |

Também se aplicam os limites/envelope gerais (413 e 500). O limitador administrativo roda antes da autorização MVC e pode retornar 429 antes da validação da chave. Sua política é separada de guidance, preservando a cota e o comportamento existentes deste. Ambas usam `RateLimiting:PermitLimit` e `RateLimiting:WindowSeconds`, mas os contadores são independentes.

## Testar o ciclo e conferir persistência

Abra `CitizenTech.Api.http`. Envie o health, depois o exemplo válido. **Troque requestId e snapshotId em cada nova solicitação**, inclusive após falha/timeout. Após um 200, consulte `GUIDANCE_RECORDS` pelo mesmo requestId e confira sessionId, horário UTC, action, result=success e errorCode nulo. Reenvio intencional do mesmo ID registrado deve retornar 409 sem nova geração. Sem conexão/tabela/permissão, deve retornar 503 sem orientação no corpo.

No modo **Fake**, o provedor espera aproximadamente 500 ms, procura elemento habilitado cujo texto seja exatamente `Agendamento` e retorna seu ID real. Caso contrário retorna `explain`, alvo nulo e mensagem de teste. O serviço valida a saída e só responde depois de `SaveChangesAsync` confirmado. Consulta de duplicidade usa LINQ `AnyAsync`; a PK também protege concorrência e ORA-00001 vira 409. Não há transação aberta durante geração, retry automático, replay nem consulta pública de registros; as consultas administrativas exigem chave.

Limites: JSON UTF-8 camelCase, 64 KiB (inclusive chunked), 80 elementos, histórico até três, prazo total 20 s incluindo leitura/consulta/geração/gravação; geração até 15 s e comandos de banco até 5 s, sempre limitados pelo orçamento restante e cancelamento. Limitação global simples da plataforma: 30 POSTs por 60 s, fila zero; ajuste `RateLimiting:PermitLimit` e `WindowSeconds` por configuração. Health não consome essa cota. Reiniciar a API reinicia o limitador em memória; não é proteção distribuída.

Erros tratados usam sempre `{contractVersion,requestId,error:{code,message}}`, incluindo JSON malformado, validação, tamanho e taxa; requestId pode ser nulo se não interpretável. Códigos: 400 INVALID_REQUEST, 409 REQUEST_ALREADY_RECORDED, 413 REQUEST_TOO_LARGE, 429 RATE_LIMITED, 502 AI_UNAVAILABLE, 503 PERSISTENCE_UNAVAILABLE, 504 AI_TIMEOUT e 500 INTERNAL_ERROR. Desconexões podem impossibilitar envio de resposta; proxies/servidores externos podem responder fora do contrato. Não há promessa de exatamente uma vez: gravação pode ter sido confirmada mesmo que o cliente perca a resposta.

Não persistimos objetivo, instrução, elementos, histórico, valores ou credenciais. `success` representa orientação válida registrada, **não tarefa concluída**. Falhas da IA não são registradas nesta etapa (registro de failure é opcional no contrato). Logs EF são desativados para evitar vazamento de informações de conexão/SQL; não habilitar sensitive-data logging nem logging de corpos.

### Roteiro de demonstração manual

1. Revise/aplique a migração somente com autorização, configure Oracle, provedor e chave administrativa no ambiente local.
2. Execute health e POST guidance do `.http`, com requestId/snapshotId novos e sessionId compartilhado. Confira o registro técnico por bind após 200.
3. Observe a jornada e envie a avaliação manual dessa sessão (não derive o outcome de `complete`). Confira a avaliação com `Data/Scripts/FindSessionEvaluation.sql`.
4. Consulte lista com todos os filtros, detalhe/timeline paginada e métricas. Ajuste a janela para incluir as datas da demonstração; compare resumo total com registros e avaliações nas respectivas janelas.
5. Se autorizado, demonstre rejeição sem chave e duplicidade de avaliação. Guarde somente evidências sanitizadas, sem segredo em prints, vídeos ou commits.

Este é um roteiro proposto, não declaração de verificações realizadas.

## Verificações desta expansão

Build da solução concluído sem avisos/erros e **187 testes aprovados** (zero falhas/ignorados), incluindo testes existentes, validação administrativa, consultas SQLite em memória e pipeline HTTP. O modelo não apresenta mudanças pendentes desde a nova migração. **Nenhuma migração foi aplicada e nenhum acesso ao Oracle real ou deploy Azure foi realizado nesta etapa.** Consulte [Data/ADMIN_VERIFICATION.md](Data/ADMIN_VERIFICATION.md) para comandos, limites das evidências e verificações pendentes no Oracle 19c.

## Critérios acadêmicos e testes

| Disciplina / requisito | Evidência |
| --- | --- |
| SOA — domínio | `Models/GuidanceRecord.cs`, invariantes de resultado/ação |
| SOA — conexão e persistência | `Data/GuidanceDbContext.cs`, `OracleGuidanceRecordStore.cs`, PK RequestId |
| SOA — migrações | `Data/Migrations`, snapshot e script SQL revisável |
| SOA — REST e documentação | controllers, contratos, transformer OpenAPI e Swagger |
| SOA / C# — testes | `CitizenTech.Api.Tests`, requisição inválida, alvo real/inexistente, persistência confirmada/falha e duplicidade |
| C# — POO, encapsulamento e interfaces | entidade, `IGuidanceProvider`, `IGuidanceRecordStore`, DI em `Program.cs` |
| C# — EF Core e LINQ | `AnyAsync`, mapeamento explícito, `SaveChangesAsync` |
| C# — async/await e cancelamento | serviço, store, provider e controller |
| C# — organização e qualidade | responsabilidades separadas, validação de entrada/saída, envelope sanitizado, testes |
| SOA — expansão administrativa | `AdminController`, quatro operações REST com chave, avaliação manual e métricas de populações distintas |
| SOA — evolução de persistência | `SessionEvaluation`, unicidade por sessão, migração gerada `20261111123553_AddSessionEvaluations` e script incremental existente revisável |
| C# — consultas e agregações | `AdminQueryService`, LINQ/EF Core assíncrono, filtros, projeções, agrupamentos e paginação |
| C# — encapsulamento e serviços | `SessionEvaluation`, `ISessionEvaluationStore`, `EvaluationService`, `AdminKeyFilter` e DI |

**Testes com doubles não comprovam integração Oracle.** O teste de confirmação usa uma gravação controlada para comprovar que o serviço aguarda sua conclusão, não que existe conexão real. É necessário executar o ciclo acima no Oracle autorizado e guardar evidência sanitizada. Os testes Gemini usam respostas HTTP simuladas e não comprovam chamada real ao Google. App, Azure e jornada no aparelho permanecem fora desta etapa.

## Equipe

| Integrante | Responsabilidade |
| --- | --- |
| A preencher | Backend C#, Oracle e integração Gemini |
| A preencher | Aplicativo Android React Native |

Página de demonstração, aparelho, endereço publicado, evidências de banco e resultados da jornada: **pendentes de confirmação pela equipe**.
