# CitizenTech — API de orientação e avaliação manual

Backend em C# utilizado nas entregas de **SOA e C#**. A CitizenTech procura reduzir a dificuldade de **idosos e pessoas com dificuldade em tecnologia** ao usar serviços em páginas web: o assistente propõe uma orientação curta por voz e, quando aplicável, um destaque visual no elemento da página. A pessoa continua responsável por tocar e preencher.


## 👥 Integrantes

| RM | Nome |
|---|---|
| 557538 | David Cordeiro     |
| 555619 | Tiago Morais       |
| 557065 | Vinicius Augusto   |
| 556892 | Guilherme Lunghini |
| 99856  | Marchel Augusto    |


## Escopo e responsabilidades

| Componente | Responsabilidade |
| --- | --- |
| App Android React Native, em outro projeto | Coletar elementos por acessibilidade, enviar a solicitação, validar a resposta, falar a instrução e desenhar o destaque sobre o Chrome. A especificação prevê Expo development build e módulo Android nativo. |
| Esta API | Receber elementos e contexto limitado, gerar orientação por Fake ou Gemini, validar a decisão e registrar metadados no Oracle antes de responder sucesso. |
| Recursos administrativos desta API | Registrar avaliação humana manual, consultar sessões e acompanhar indicadores, mediante chave administrativa. |

**A API não lê diretamente o celular, não desenha overlays, não executa cliques e não confirma agendamentos pelo cidadão.** O aplicativo não está neste repositório. Não há, nesta revisão documental, evidência verificada de integração com o app, implantação Azure ou resultados com participantes.

O contrato mobile é a [especificação v1, revisão 1.1 de 07/11/2026](CitizenTech.Api/Especificacao_App_CitizenTech_MVP.md). A expansão administrativa é separada e não altera os dois endpoints originais. Não há frontend administrativo, login, usuários ou perfis.

## Tecnologias verificadas

Versões obtidas dos projetos e do manifesto de ferramentas, não de relatórios anteriores:

| Tecnologia | Versão/configuração |
| --- | --- |
| .NET / ASP.NET Core | Target `net10.0`, aplicação Web com controllers; requer SDK .NET 10 para desenvolvimento |
| EF Core / Relational / Design | `10.0.14` |
| Oracle.EntityFrameworkCore | `10.23.26400` |
| Banco de destino | **Oracle 19c, confirmado pela equipe**; runtime e factory usam `DatabaseVersion19` |
| OpenAPI nativo | `Microsoft.AspNetCore.OpenApi 10.0.14` |
| Swagger UI | `Swashbuckle.AspNetCore 10.1.0` |
| Integração Gemini | HTTP `generateContent`, JSON com `responseJsonSchema`; sem SDK Google |
| Dependência criptográfica direta | `System.Security.Cryptography.Pkcs 10.0.14` |
| Testes | xUnit `2.9.3`, runner VS `3.1.4`, Test SDK `17.14.1` |
| Testes HTTP / consultas | `Microsoft.AspNetCore.Mvc.Testing 10.0.0`, `Microsoft.EntityFrameworkCore.Sqlite 10.0.14` |
| Cobertura / ferramenta EF | `coverlet.collector 6.0.4`, `dotnet-ef 10.0.14` |

Fontes: [API.csproj](CitizenTech.Api/CitizenTech.Api.csproj), [testes.csproj](CitizenTech.Api.Tests/CitizenTech.Api.Tests.csproj) e [dotnet-tools.json](CitizenTech.Api/dotnet-tools.json).

## Organização

```text
CitizenTech.slnx
README.md                         documentação principal
CitizenTech.Api/
  Contracts/                      DTOs e validação das entradas
  Controllers/                    seis operações HTTP
  Models/                         entidades com invariantes e encapsulamento
  Services/                       casos de uso, provedores e consultas LINQ
  Data/                           DbContext, stores e factory design-time
    Migrations/                   migrações e snapshot EF
    Scripts/                      SQL de criação/delta e consultas por bind
  Errors/                         envelope, middleware, chave admin e OpenAPI
  Properties/                     perfis locais de execução
  Program.cs                      DI, pipeline e limitação de requisições
  dotnet-tools.json               manifesto local do dotnet-ef
  CitizenTech.Api.http             exemplos manuais
  Especificacao_App_CitizenTech_MVP.md
CitizenTech.Api.Tests/             testes unitários, HTTP e consultas SQLite
```

## Configuração obrigatória: variáveis de ambiente

> **O repositório não fornece credenciais. Configure seus próprios valores fora do Git.** Nunca coloque chave ou senha real no README, appsettings, `launchSettings.json`, arquivos `.http`, scripts, testes, prints, vídeos ou commits. Os valores abaixo são apenas placeholders.

Dois sublinhados (`__`) representam a hierarquia de configuração do ASP.NET Core: `Admin__ApiKey` corresponde a `Admin:ApiKey`.

| Variável | Finalidade | Necessidade / padrão verificado | Sensível? |
| --- | --- | --- | --- |
| `ConnectionStrings__Oracle` | Conexão da API ao Oracle | Necessária para guidance bem-sucedido, avaliações e consultas; padrão vazio | **Sim**, contém credenciais |
| `Guidance__Provider` | Seleciona `Fake` ou `Gemini` (grafia exata) | Padrão versionado **Fake**; selecionar Gemini explicitamente na hospedagem | Não |
| `Gemini__ApiKey` | Chave do Google, apenas no servidor | Necessária no modo Gemini; padrão vazio | **Sim** |
| `Gemini__Model` | Identificador do modelo | Padrão `gemini-3.5-flash-lite`; necessário modelo compatível no modo Gemini | Não |
| `Admin__ApiKey` | Chave dos quatro endpoints administrativos | Necessária para acessá-los; sem configuração retorna 503 e não libera acesso | **Sim** |
| `ASPNETCORE_ENVIRONMENT` | Ambiente ASP.NET Core | `Development` local; **Production** na hospedagem | Não |
| `RateLimiting__PermitLimit` | Permissões por janela, por política | Padrão `30`; inteiro positivo | Não |
| `RateLimiting__WindowSeconds` | Duração da janela | Padrão `60`; inteiro positivo | Não |

Exemplo PowerShell, no terminal que iniciará a API, a partir da raiz:

```powershell
$env:ConnectionStrings__Oracle = 'User Id=<USUARIO>;Password=<SENHA>;Data Source=<HOST>:<PORTA>/<SERVICO>;Connection Timeout=5;'
$env:Guidance__Provider = 'Gemini'
$env:Gemini__ApiKey = '<SUA_CHAVE_GEMINI>'
$env:Gemini__Model = '<MODELO_COMPATIVEL>'
$env:Admin__ApiKey = '<SUA_CHAVE_ADMINISTRATIVA>'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
```

O formato Oracle acima usa **nome de serviço**, não SID. Substitua os placeholders somente no ambiente local. A compatibilidade Oracle 19c informada não comprova conexão, privilégios ou aplicação de migração.

Cuidados operacionais:

- `$env:` define variáveis temporárias da sessão; processos iniciados nesse terminal herdam seus valores. Reinicie a API após mudanças.
- F5 no Visual Studio **não herda variáveis definidas posteriormente em um terminal filho**. Configure o ambiente do processo que inicia o Visual Studio ou use User Secrets.
- Variáveis de ambiente podem sobrepor User Secrets na configuração padrão. Valores antigos podem mascarar uma alteração em secrets.
- Comandos contendo segredos podem permanecer no histórico do PowerShell. Não compartilhe esse histórico nem use comandos que imprimam conexão/chave para conferir configuração.
- O projeto **não implementa carregamento automático de `.env`**.

### User Secrets como alternativa local persistente

O projeto já possui `UserSecretsId`. User Secrets é armazenamento de configuração de desenvolvimento **fora do repositório**, não variável de ambiente nem cofre de produção. Evite expor os valores e prefira também a opção **Gerenciar Segredos do Usuário** do Visual Studio.

Comandos com placeholders, a partir da raiz (os comandos reais também podem ficar no histórico):

```powershell
dotnet user-secrets set 'ConnectionStrings:Oracle' 'User Id=<USUARIO>;Password=<SENHA>;Data Source=<HOST>:<PORTA>/<SERVICO>;Connection Timeout=5;' --project CitizenTech.Api/CitizenTech.Api.csproj
dotnet user-secrets set 'Gemini:ApiKey' '<SUA_CHAVE_GEMINI>' --project CitizenTech.Api/CitizenTech.Api.csproj
dotnet user-secrets set 'Admin:ApiKey' '<SUA_CHAVE_ADMINISTRATIVA>' --project CitizenTech.Api/CitizenTech.Api.csproj
```

Não execute `user-secrets list` para produzir prints de configuração. User Secrets não é enviado automaticamente para Azure.

## Execução local

Pré-requisitos: SDK .NET 10; acesso e credenciais próprios do Oracle; chave Google se usar Gemini. Os testes automatizados não exigem essas integrações reais.

Na **raiz da solução**:

```powershell
dotnet restore CitizenTech.slnx
dotnet build CitizenTech.slnx
dotnet test CitizenTech.slnx
```

O manifesto fica diretamente dentro de `CitizenTech.Api`, não em `.config`. Restaure-o explicitamente a partir da raiz:

```powershell
dotnet tool restore --tool-manifest CitizenTech.Api/dotnet-tools.json
```

Depois de configurar as variáveis **no mesmo terminal**, entre na pasta da API e inicie usando a porta dos exemplos:

```powershell
Set-Location CitizenTech.Api
dotnet run --no-launch-profile --urls http://localhost:5075
```

| Recurso local | Endereço |
| --- | --- |
| Health | http://localhost:5075/api/v1/health |
| Swagger UI, somente Development | http://localhost:5075/swagger |
| Documento OpenAPI, somente Development | http://localhost:5075/openapi/v1.json |

`--no-launch-profile` evita usar as portas/variáveis dos perfis de execução. O pipeline tem redirecionamento HTTPS; o comando HTTP acima é apenas para desenvolvimento local, sem porta HTTPS definida por esse comando. Não reproduza essa configuração como publicação.

**Celular físico:** `localhost` no aparelho aponta para o próprio celular. O acesso ao computador depende do IP local, da interface em que a API escuta e da liberação da porta no firewall/rede. O endereço acima escuta em localhost; para acesso externo local é necessário ajustar explicitamente a escuta e a baseUrl do app. Não desabilite validação TLS para contornar erro de certificado. CORS não resolve bloqueio de rede de aplicativo nativo.

## Fake e Gemini

| Provedor | Comportamento |
| --- | --- |
| `Fake` | Padrão em `appsettings.json`; não chama Google. Espera aproximadamente 500 ms e procura elemento habilitado cujo texto seja exatamente `Agendamento`; retorna highlight para o ID recebido ou explain de teste. **Continua exigindo gravação Oracle para HTTP 200.** |
| `Gemini` | Chave apenas no servidor; faz uma chamada HTTP com saída estruturada e validação posterior de ação, instrução e alvo. Não há fallback automático para Fake, retries ou redirects no cliente. |

Seleção ausente/inválida não ativa Fake implicitamente. Gemini requer modelo/configuração compatíveis com saída estruturada; schema válido **não garante correção semântica** da orientação. A consulta Oracle acontece antes da geração: falha nela pode retornar 503 antes de qualquer acesso ao Google.

O modelo atualmente configurado é **`gemini-3.5-flash-lite`**. A [referência oficial do modelo](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite), consultada em 11/11/2026, o lista como Stable e com suporte a saída estruturada. Verifique [saída estruturada](https://ai.google.dev/gemini-api/docs/structured-output), [generateContent](https://ai.google.dev/api/generate-content), [preços](https://ai.google.dev/gemini-api/docs/pricing) e [limites](https://ai.google.dev/gemini-api/docs/rate-limits) ao configurar a conta. **Não há promessa de disponibilidade ou gratuidade permanente; chamadas reais podem consumir quota ou gerar cobrança conforme o plano.**

Health não consulta Oracle nem Gemini e não comprova suas disponibilidades. Selecionar `Gemini` explicitamente na hospedagem evita apresentar o padrão Fake como IA real.

## Endpoints v1

Todos usam JSON camelCase. A baseUrl é a raiz do servidor, sem `/api/v1`.

| Método e rota | Finalidade | `X-Admin-Key` | Principais HTTP |
| --- | --- | --- | --- |
| `GET /api/v1/health` | Disponibilidade/versão da aplicação | Não | 200 |
| `POST /api/v1/guidance` | Uma orientação com metadados persistidos | Não | 200, 400, 409, 413, 429, 502, 503, 504 |
| `POST /api/v1/sessions/{sessionId}/evaluation` | Avaliação manual única | **Sim** | 201, 400, 401, 404, 409, 429, 503 |
| `GET /api/v1/admin/sessions` | Resumos de sessões com filtros | **Sim** | 200, 400, 401, 429, 503 |
| `GET /api/v1/admin/sessions/{sessionId}` | Resumo, avaliação e timeline | **Sim** | 200, 400, 401, 404, 429, 503 |
| `GET /api/v1/admin/metrics` | Agregações técnicas e de avaliações | **Sim** | 200, 400, 401, 429, 503 |

Erros internos tratados podem retornar 500. Chave administrativa ausente, errada, múltipla ou ambígua retorna 401 `UNAUTHORIZED`; servidor sem chave configurada retorna 503 `ADMIN_NOT_CONFIGURED`. Use somente o header, nunca URL/query string. A chave não é autenticação de cidadãos nem deve ir para o APK.

### Orientação: requisição completa

Exemplo fictício para `POST http://localhost:5075/api/v1/guidance`, com `Content-Type: application/json`:

```json
{
  "contractVersion": "1",
  "requestId": "a1111111-1111-4111-8111-111111111111",
  "sessionId": "b2222222-2222-4222-8222-222222222222",
  "locale": "pt-BR",
  "goal": "Quero agendar a renovação do meu RG",
  "userMessage": null,
  "history": [],
  "screen": {
    "snapshotId": "c3333333-3333-4333-8333-333333333333",
    "packageName": "com.android.chrome",
    "elements": [
      {"id":"e1","role":"button","text":"Agendamento","enabled":true},
      {"id":"e2","role":"link","text":"Consultar solicitação","enabled":true},
      {"id":"e3","role":"input","text":"Município","enabled":true}
    ]
  }
}
```

Formato de resposta 200 (texto ilustrativo, não resultado de execução real Gemini):

```json
{
  "contractVersion": "1",
  "requestId": "a1111111-1111-4111-8111-111111111111",
  "sessionId": "b2222222-2222-4222-8222-222222222222",
  "snapshotId": "c3333333-3333-4333-8333-333333333333",
  "action": "highlight",
  "instruction": "Toque no botão Agendamento destacado.",
  "targetElementId": "e1"
}
```

| Identificador | Significado |
| --- | --- |
| `requestId` | UUID novo por solicitação; único no Oracle. Duplicidade retorna 409, sem replay da orientação. |
| `sessionId` | UUID criado pelo app para correlacionar a ajuda; não é usuário nem sessão autenticada. Pode agrupar vários guidances. |
| `snapshotId` | UUID da leitura atual; o app deve descartar resposta incompatível ou atrasada após mudança da tela. |

Use **novos requestId e snapshotId em toda nova solicitação**, inclusive tentativas após falha. `history` contém até três orientações válidas; `userMessage` é obrigatório mas nullable. Limites: 64 KiB, 1–80 elementos, objetivo/mensagem até 500 caracteres, texto de elemento até 160 e instrução de saída até 250. Ações: `highlight`, `explain`, `ask`, `complete`; só highlight admite alvo, que deve existir e estar habilitado. Não enviar senha, valores de formulário, screenshot, HTML ou árvore bruta.

### Avaliação e consultas administrativas

Exemplos para clientes compatíveis com arquivos `.http`/REST Client; `{{$processEnv Admin__ApiKey}}` referencia o ambiente do processo cliente, não uma chave literal. Reinicie o cliente a partir do ambiente configurado se necessário. No Swagger Development, use **Authorize** para informar X-Admin-Key somente nas operações protegidas.

```http
@baseUrl = http://localhost:5075
@sessionId = b2222222-2222-4222-8222-222222222222

### Após um guidance persistido para a sessão
POST {{baseUrl}}/api/v1/sessions/{{sessionId}}/evaluation
X-Admin-Key: {{$processEnv Admin__ApiKey}}
Content-Type: application/json

{
  "outcome": "completed",
  "rating": 4,
  "neededHumanHelp": true
}

### Lista com todos os filtros
GET {{baseUrl}}/api/v1/admin/sessions?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z&page=1&pageSize=20&hasFailures=false&hasEvaluation=true&outcome=completed
X-Admin-Key: {{$processEnv Admin__ApiKey}}

### Detalhe: paginação da timeline, sem intervalo
GET {{baseUrl}}/api/v1/admin/sessions/{{sessionId}}?page=1&pageSize=20
X-Admin-Key: {{$processEnv Admin__ApiKey}}

### Indicadores
GET {{baseUrl}}/api/v1/admin/metrics?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z
X-Admin-Key: {{$processEnv Admin__ApiKey}}
```

A avaliação exige sessão com pelo menos um registro em GUIDANCE_RECORDS, UUID não vazio, `outcome` exatamente `completed`, `partially_completed` ou `not_completed`, nota inteira 1–5 e booleano **obrigatório** `neededHumanHelp`. Campos ausentes, null indevido ou extras são rejeitados; não aceita comentário ou identificação do participante. O servidor define UUID, UTC e `source="manual"`. Retorna 201 com `id`, `sessionId`, `outcome`, `rating`, `neededHumanHelp`, `source`, `createdAtUtc`, **sem Location inventado**. Não há edição/exclusão; duplicidade, inclusive na gravação concorrente, retorna 409.

**Intervalos de lista/métricas:** `from` inclusivo e `to` exclusivo, normalizados para UTC. Exigem horário com segundos e timezone explícito (`Z` ou `±HH:mm`); codifique `+` como `%2B` na query. Informe ambos ou nenhum. O padrão é `[agora UTC - 30 dias, agora UTC)`, calculando agora uma vez. Um limite isolado, falta de timezone ou `from >= to` retorna 400; a resposta inclui `from`/`to` efetivos.

**Lista:** seleciona sessões com algum registro técnico na janela, mas resume **todos os registros da sessão**. `hasFailures` filtra esse resumo total; `hasEvaluation` e `outcome` filtram a avaliação existente, independentemente de sua data. `hasEvaluation=false` com outcome é inválido. Cada item tem `sessionId`, `firstRecordedAtUtc`, `lastRecordedAtUtc`, `totalGuidanceRecords`, `successfulRecords`, `failedRecords`, `hasEvaluation`, `outcome` e `rating` nullable. Ordenação por última atividade decrescente, depois sessionId.

**Paginação:** `page=1` e `pageSize=20`, tamanho máximo 100 e mínimo 1; página positiva e deslocamento dentro de Int32. Retorna `items`, `page`, `pageSize`, `totalItems`, `totalPages` (zero se vazio). Página além do fim retorna itens vazios.

**Detalhe:** `session` com resumo completo, `evaluation` separado (ou null), `timeline` paginada. A timeline só contém `requestId`, `createdAtUtc`, `action`, `result`, `errorCode`, ordenados por data crescente e requestId. Sessão inexistente retorna 404; UUID/paginação inválidos, 400.

### Envelope de erro

Exemplo administrativo 401; o UUID é ilustrativo:

```json
{
  "contractVersion": "1",
  "requestId": "d4444444-4444-4444-8444-444444444444",
  "error": {
    "code": "UNAUTHORIZED",
    "message": "Acesso administrativo não autorizado."
  }
}
```

Nos quatro endpoints administrativos, `requestId` do envelope é correlação gerada pelo servidor, não ID enviado pelo avaliador. No guidance preserva o requestId recebido quando interpretável, podendo ser null. Não são retornados stack traces, detalhes da chave esperada ou respostas brutas do banco/provedor.

| Código | HTTP / significado |
| --- | --- |
| `INVALID_REQUEST` | 400, entrada inválida |
| `UNAUTHORIZED` | 401, chave inválida/ausente/ambígua |
| `SESSION_NOT_FOUND` | 404, sessão sem registro técnico |
| `REQUEST_ALREADY_RECORDED` / `SESSION_ALREADY_EVALUATED` | 409, solicitação/avaliação duplicada |
| `REQUEST_TOO_LARGE` / `RATE_LIMITED` | 413 / 429, limites locais |
| `ADMIN_NOT_CONFIGURED` / `PERSISTENCE_UNAVAILABLE` | 503, administração não configurada / persistência indisponível |
| `AI_UNAVAILABLE` / `AI_TIMEOUT` | 502 / 504, geração indisponível/inválida / timeout |
| `INTERNAL_ERROR` | 500, erro interno sanitizado |

Um HTTP 429 do Google é convertido em 502 `AI_UNAVAILABLE`, diferente do 429 do limitador local. Proxies/servidores externos podem responder fora do envelope.

### Limites operacionais

Duas políticas independentes em memória: guidance e administração, cada uma com padrão de 30 chamadas/60 s, fila zero. A cota administrativa é compartilhada pelos quatro endpoints; **tentativas com chave inválida contam**, podendo receber 429 antes da autorização. Health não consome cota. Não há particionamento por IP/usuário nem proteção distribuída; reiniciar zera os contadores.

Guidance tem orçamento total de 20 s, geração até 15 s e reserva para persistência; comandos Oracle têm timeout de 5 s. Cancelamento é propagado. Uma conexão interrompida pode deixar escrita confirmada sem resposta recebida: não há garantia de execução exatamente uma vez, replay ou retry automático.

## Oracle e migrações

| Tabela | Conteúdo e garantia |
| --- | --- |
| `GUIDANCE_RECORDS` | RequestId (PK), SessionId, UTC, Action, Result e ErrorCode. Um registro por requestId, múltiplos por sessão. |
| `SESSION_EVALUATIONS` | Id (PK), SessionId único, Outcome, Rating, NeededHumanHelp, Source e UTC. Uma avaliação manual por sessão. |

**Guidance só retorna 200 após confirmar a gravação; avaliação só retorna 201 após confirmar a gravação. Falha de banco impede sucesso.** A consulta prévia não substitui as restrições únicas. Não há tabela de usuários/sessões nem FK de avaliação para SessionId não único em GUIDANCE_RECORDS; existência é validada no serviço.

As tabelas não armazenam conteúdo da página, objetivo, instruções, prints ou credenciais. UUIDs usam `VARCHAR2(36)` e datas `TIMESTAMP(7)` com convenção UTC. Avaliação usa `NUMBER(2)` para nota e `NUMBER(1)` para booleano numérico, compatíveis com o mapeamento Oracle 19c.

**Não há aplicação automática de migrações, EnsureCreated ou seed na inicialização.** Arquivos existentes não comprovam aplicação no Oracle. Antes de qualquer alteração, confira esquema, privilégios, objetos e `__EFMigrationsHistory` com o responsável. Pare diante de objetos conflitantes; não apague/recrie tabelas compartilhadas.

| Migração / artefato | Caminho |
| --- | --- |
| `20261110134447_InitialGuidanceRecords` | [Migração inicial](CitizenTech.Api/Data/Migrations/20261110134447_InitialGuidanceRecords.cs) |
| `20261111123553_AddSessionEvaluations` | [Migração de avaliações](CitizenTech.Api/Data/Migrations/20261111123553_AddSessionEvaluations.cs) |
| Script inicial, inclui histórico EF | [001_InitialGuidanceRecords.sql](CitizenTech.Api/Data/Scripts/001_InitialGuidanceRecords.sql) |
| Delta inicial → avaliações, sem recriar guidance | [002_SessionEvaluations.sql](CitizenTech.Api/Data/Scripts/002_SessionEvaluations.sql) |
| Conferência por requestId | [FindGuidanceRecord.sql](CitizenTech.Api/Data/Scripts/FindGuidanceRecord.sql), bind `:request_id` |
| Conferência por sessionId e duplicidades | [FindSessionEvaluation.sql](CitizenTech.Api/Data/Scripts/FindSessionEvaluation.sql), bind `:session_id` |

### 1. Preparação de banco novo

Após revisão/autorização, escolha **EF OU scripts**, nunca ambos indiscriminadamente. Os comandos a seguir são instruções manuais, **não relato de execução**.

Opção EF, na pasta `CitizenTech.Api`, com conexão externa configurada:

```powershell
dotnet ef database update 20261111123553_AddSessionEvaluations
```

Aplica ambas as migrações pendentes. Alternativa por scripts: revisar e aplicar `001_InitialGuidanceRecords.sql`, depois `002_SessionEvaluations.sql`, verificando o histórico entre etapas. Escolha ferramenta Oracle que respeite os delimitadores dos scripts e o procedimento autorizado pela equipe.

### 2. Banco que já possui a migração inicial

Confirme a entrada `20261110134447_InitialGuidanceRecords` no histórico. Não reaplique 001. Use o mesmo comando EF acima para aplicar **somente pendências**, ou aplique **apenas 002** com ferramenta Oracle após revisão. Se avaliações já constar no histórico, não reaplique o delta.

Para reproduzir os scripts para revisão, na pasta da API (estes comandos geram arquivos, não atualizam o banco):

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef migrations script 0 20261110134447_InitialGuidanceRecords --output Data/Scripts/001_InitialGuidanceRecords.sql
dotnet ef migrations script 20261110134447_InitialGuidanceRecords 20261111123553_AddSessionEvaluations --output Data/Scripts/002_SessionEvaluations.sql
```

A factory design-time lê appsettings da pasta atual, User Secrets e ambiente; pode gerar SQL offline sem abrir conexão. Não use `database update` como diagnóstico casual. Confirme gravação e unicidade com os binds das consultas sanitizadas, sem expor conexão/senha. Não execute Down em banco compartilhado.

## Indicadores: significado e limites

A resposta de métricas inclui intervalo efetivo, contagens e erros agrupados. Há **duas bases temporais diferentes**, ambas usando `[from,to)`:

1. Técnica: GUIDANCE_RECORDS criados no intervalo. `totalSessions` conta sessionIds distintos nesses registros.
2. Avaliação: SESSION_EVALUATIONS criadas no intervalo, **não filtradas pela data de início ou atividade da sessão**.

Não se calcula conversão entre essas populações.

| Campo | Fórmula / base |
| --- | --- |
| `totalGuidanceRecords`, `successfulRecords`, `failedRecords` | Registros totais, success e failure na base técnica |
| `technicalSuccessRate` | `100 × successfulRecords / totalGuidanceRecords` |
| `totalEvaluations` e contagens por outcome | Total recebido; `completedEvaluations`, `partiallyCompletedEvaluations`, `notCompletedEvaluations` |
| `completionRate` | `100 × completedEvaluations / totalEvaluations` |
| `neededHumanHelpEvaluations` / `humanHelpRate` | Contagem de true / `100 × neededHumanHelpEvaluations / totalEvaluations` |
| `averageRating` | Soma das notas / totalEvaluations |
| `errorCodes` | Registros failure com errorCode não nulo, agrupados e ordenados por código, formato `{value,count}` |

Percentuais de 0 a 100 e média são arredondados a duas casas, ponto médio afastado de zero. **Denominador zero retorna null**, acompanhado de contagens zero; média sem avaliações também é null. Erros sem registros resultam em lista vazia.

Limitações importantes:

- Sucesso técnico não comprova conclusão da tarefa; `action=complete` não substitui avaliação humana.
- Sessões sem avaliação **não são fracassos**. Resultados e necessidade de ajuda são informados manualmente pelo avaliador, sujeitos à seleção e relato.
- O modelo permite registros failure, mas **o fluxo atual de GuidanceService grava somente sucessos; não grava falhas de geração ou persistência**. Portanto, os indicadores não cobrem todas as tentativas/falhas reais. Falhas que não chegaram ao Oracle não aparecem.
- Primeiro e último registro representam atividade observada pela API, **não necessariamente duração da tarefa**.
- Consultas usam agregações LINQ/EF assíncronas; não há chamada Gemini para calcular indicadores.

## Testes e evidências

Execução desta revisão em **11/11/2026**, a partir da raiz:

```powershell
dotnet test CitizenTech.slnx
```

Resultado real: **187 aprovados, 0 falhas, 0 ignorados**. O comando restaurou e compilou os projetos antes dos testes. Essa contagem foi obtida nesta execução, não copiada do relatório anterior.

| Grupo existente em `CitizenTech.Api.Tests/` | Escopo / tipo de evidência |
| --- | --- |
| [GuidanceRequestTests](CitizenTech.Api.Tests/GuidanceRequestTests.cs) | Validação e limites do contrato |
| [GuidanceServiceTests](CitizenTech.Api.Tests/GuidanceServiceTests.cs) | Doubles: decisões, confirmação aguardada, falha, duplicidade e orçamento |
| [GuidanceRegistrationTests](CitizenTech.Api.Tests/GuidanceRegistrationTests.cs) | DI, seleção/configuração de provedor e health |
| [GeminiGuidanceProviderTests](CitizenTech.Api.Tests/GeminiGuidanceProviderTests.cs) | HTTP Google simulado, payload/schema, respostas, erros, timeout e ausência de fallback |
| [AdminTests](CitizenTech.Api.Tests/AdminTests.cs) | Avaliação e conflitos com doubles, autorização, contratos, relógio/intervalos e paginação |
| [AdminHttpTests](CitizenTech.Api.Tests/AdminHttpTests.cs) | Pipeline HTTP com WebApplicationFactory, envelope, limites, autorização e OpenAPI |
| [AdminQueryTests](CitizenTech.Api.Tests/AdminQueryTests.cs) | Consultas relacionais SQLite em memória: filtros, agregações, bases distintas e paginação |

**Doubles e SQLite não comprovam compatibilidade Oracle.** A execução acima não realizou integração real Oracle, chamada real Gemini, jornada no app ou deploy Azure. Permanecem necessários testes autorizados de migrações, materialização UTC/tipos, tradução/execução LINQ Oracle, concorrência ORA-00001 e indisponibilidade real. Evidências reais Gemini e mobile devem ser registradas separadamente, sem segredos nem dados pessoais.

### Roteiro de demonstração manual (a executar)

1. Preparar o esquema autorizado, configurar ambiente e identificar claramente Fake ou Gemini.
2. **Health:** confirmar versão/disponibilidade da API, sem concluir que dependências funcionam.
3. **Guidance:** enviar dados fictícios com IDs novos e aguardar 200.
4. **Conferência Oracle:** consultar por requestId e conferir sessionId, UTC, action, success e errorCode nulo.
5. **Avaliação:** após observar a jornada, enviar resultado manual para a mesma sessão; aguardar 201 e conferir registro por sessionId.
6. **Listagem/detalhamento:** consultar filtros, resumo completo e timeline paginada.
7. **Métricas:** usar janela que inclua os instantes desejados e comparar contagens nas duas bases temporais.
8. Registrar evidências sanitizadas; avaliação técnica ou simulada não deve ser apresentada como resultado obtido com participantes.

### Critérios SOA/C# e localização das evidências

Caminhos de implementação abaixo são relativos a `CitizenTech.Api/`; testes estão em `CitizenTech.Api.Tests/`.

| Critério | Arquivos / implementação | Testes correspondentes |
| --- | --- | --- |
| SOA: contrato REST e validação | `Controllers/`, `Contracts/`, `Errors/ContractOpenApiTransformer.cs` | GuidanceRequestTests, AdminHttpTests |
| SOA: integração externa | `Services/GeminiGuidanceProvider.cs`, `GuidanceRegistration.cs` | GeminiGuidanceProviderTests, GuidanceRegistrationTests (simulados) |
| SOA: persistência e evolução | `Data/GuidanceDbContext.cs`, stores, `Data/Migrations/`, `Data/Scripts/` | GuidanceServiceTests, AdminTests; Oracle real pendente |
| SOA: acompanhamento administrativo | `Controllers/AdminController.cs`, `Errors/AdminKeyFilter.cs` | AdminTests, AdminHttpTests |
| C#: POO e encapsulamento | `Models/GuidanceRecord.cs`, `SessionEvaluation.cs`, interfaces de provider/store e DI | GuidanceServiceTests, AdminTests |
| C#: EF Core e LINQ | `Services/AdminQueryService.cs`, `Data/OracleGuidanceRecordStore.cs` | AdminQueryTests (SQLite, não Oracle) |
| C#: async/await e CancellationToken | Controllers, serviços, stores e provider | GuidanceServiceTests, GeminiGuidanceProviderTests, AdminTests |
| C#: tratamento de erros | `Errors/ContractMiddleware.cs`, `Contracts/Responses.cs` | AdminHttpTests e testes de serviços |

## Preparação para Azure (sem deploy nesta revisão)

Serviço planejado: **Azure App Service**. Antes de publicar:

- Verificar suporte ao runtime **.NET 10** na plataforma escolhida e definir processo de publicação compatível.
- Configurar novamente conexão, provedor, modelo e chaves no ambiente Azure. Variáveis do computador e User Secrets **não são enviados automaticamente**.
- Usar **Production** e HTTPS. Swagger/OpenAPI só são mapeados em Development no código atual; **não usar Development em produção apenas para expor documentação**. Este README e a especificação permanecem disponíveis no repositório.
- Selecionar Gemini explicitamente se a demonstração exigir IA real; o padrão versionado é Fake.
- Validar conectividade/permissões Oracle e acesso/quota Gemini **a partir da Azure**. Health sozinho não comprova essas dependências.
- Planejar aplicação manual revisada das migrações e lembrar que os contadores de rate limiting são locais à instância, não distribuídos.
- Manter segredos fora do Git, dos perfis de publicação e de evidências. Não anexar credenciais em chamados ou vídeos.

Nenhum recurso Azure foi criado nesta tarefa. URL publicada, estado de publicação e evidências de acesso hospedado: **A preencher**.

## Equipe e informações pendentes

| Integrante | Identificação | Responsabilidade |
| --- | --- | --- |
| David (nome confirmado na especificação) | Nome completo / RM: **A preencher** | Backend C#, Gemini, Oracle, migrações e preparação Azure |
| **A preencher** | Nome / RM: **A preencher** | Aplicativo React Native e módulo Android, voz, acessibilidade, overlay e APK |
| Ambos | — | Integração do contrato e evidências da jornada no aparelho |

A equipe ainda precisa preencher: URL do repositório mobile, URL publicada (quando houver), aparelho/Android/Chrome, página e cenário de demonstração, evidências reais Oracle/Gemini/app, participantes/resultados efetivamente coletados e links de vídeos. Não há resultados com usuários declarados neste documento.

## Documentação relacionada

- [Especificação original mobile/API v1, revisão 1.1](CitizenTech.Api/Especificacao_App_CitizenTech_MVP.md) — contrato original, sem alterações nesta tarefa.
- [Guia técnico interno da API](CitizenTech.Api/README.md) — pontos de implementação e operação a partir da pasta da API.
- [Detalhes da camada de persistência](CitizenTech.Api/Data/README.md).
- [Exemplos HTTP](CitizenTech.Api/CitizenTech.Api.http) — dados fictícios, chave administrativa referenciada pelo ambiente.
