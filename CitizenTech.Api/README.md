# CitizenTech API — Oracle e Gemini

Backend do assistente para idosos e pessoas com dificuldade em tecnologia. O app Android lê elementos da tela no Chrome e recebe uma orientação e, quando aplicável, o ID real do elemento a destacar. A API não toca, não preenche e não confirma agendamentos pelo cidadão.

**Estado atual:** orientação por **Fake ou Gemini**, selecionada explicitamente em `Guidance:Provider`; persistência **Oracle real e obrigatória** em ambos os modos. A integração e migração Oracle da etapa anterior foram preservadas, sem alterações de tabelas. Não há publicação Azure, autenticação nem app neste projeto. A especificação `Especificacao_App_CitizenTech_MVP.md`, revisão 1.1, continua sendo a fonte do contrato. A chamada real ao Google depende de teste manual autorizado; não foi executada automaticamente.

## Estrutura

```text
CitizenTech.slnx                         (na pasta superior, solução existente)
CitizenTech.Api/
  Controllers/GuidanceController.cs      POST guidance e GET health
  Contracts/                            requisição, respostas e validação
  Models/GuidanceRecord.cs               metadados e invariantes encapsulados
  Data/                                 DbContext, store Oracle, factory e migração
    Migrations/                         migração inicial e snapshot EF
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
dotnet ef migrations script --output Data/Migrations/initial.sql
```

5. Somente após revisão e autorização, com a versão validada e conexão configurada, aplicar explicitamente:

```powershell
dotnet ef database update
```

Verifique as instruções da factory: comandos design-time usam conexão de ambiente/User Secrets quando configurada; sem conexão, a aplicação da migração falha sem acessar host fictício. O script gerado inicialmente está em `Data/Scripts/001_InitialGuidanceRecords.sql` e a consulta por `REQUEST_ID` em `Data/Scripts/FindGuidanceRecord.sql`. Use o UUID textual do POST como bind, sem divulgar dados de conexão.

## Testar o ciclo e conferir persistência

Abra `CitizenTech.Api.http`. Envie o health, depois o exemplo válido. **Troque requestId e snapshotId em cada nova solicitação**, inclusive após falha/timeout. Após um 200, consulte `GUIDANCE_RECORDS` pelo mesmo requestId e confira sessionId, horário UTC, action, result=success e errorCode nulo. Reenvio intencional do mesmo ID registrado deve retornar 409 sem nova geração. Sem conexão/tabela/permissão, deve retornar 503 sem orientação no corpo.

No modo **Fake**, o provedor espera aproximadamente 500 ms, procura elemento habilitado cujo texto seja exatamente `Agendamento` e retorna seu ID real. Caso contrário retorna `explain`, alvo nulo e mensagem de teste. O serviço valida a saída e só responde depois de `SaveChangesAsync` confirmado. Consulta de duplicidade usa LINQ `AnyAsync`; a PK também protege concorrência e ORA-00001 vira 409. Não há transação aberta durante geração, retry automático, replay nem consulta pública de registros.

Limites: JSON UTF-8 camelCase, 64 KiB (inclusive chunked), 80 elementos, histórico até três, prazo total 20 s incluindo leitura/consulta/geração/gravação; geração até 15 s e comandos de banco até 5 s, sempre limitados pelo orçamento restante e cancelamento. Limitação global simples da plataforma: 30 POSTs por 60 s, fila zero; ajuste `RateLimiting:PermitLimit` e `WindowSeconds` por configuração. Health não consome essa cota. Reiniciar a API reinicia o limitador em memória; não é proteção distribuída.

Erros tratados usam sempre `{contractVersion,requestId,error:{code,message}}`, incluindo JSON malformado, validação, tamanho e taxa; requestId pode ser nulo se não interpretável. Códigos: 400 INVALID_REQUEST, 409 REQUEST_ALREADY_RECORDED, 413 REQUEST_TOO_LARGE, 429 RATE_LIMITED, 502 AI_UNAVAILABLE, 503 PERSISTENCE_UNAVAILABLE, 504 AI_TIMEOUT e 500 INTERNAL_ERROR. Desconexões podem impossibilitar envio de resposta; proxies/servidores externos podem responder fora do contrato. Não há promessa de exatamente uma vez: gravação pode ter sido confirmada mesmo que o cliente perca a resposta.

Não persistimos objetivo, instrução, elementos, histórico, valores ou credenciais. `success` representa orientação válida registrada, **não tarefa concluída**. Falhas da IA não são registradas nesta etapa (registro de failure é opcional no contrato). Logs EF são desativados para evitar vazamento de informações de conexão/SQL; não habilitar sensitive-data logging nem logging de corpos.

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

**Testes com doubles não comprovam integração Oracle.** O teste de confirmação usa uma gravação controlada para comprovar que o serviço aguarda sua conclusão, não que existe conexão real. É necessário executar o ciclo acima no Oracle autorizado e guardar evidência sanitizada. Os testes Gemini usam respostas HTTP simuladas e não comprovam chamada real ao Google. App, Azure e jornada no aparelho permanecem fora desta etapa.

## Equipe

| Integrante | Responsabilidade |
| --- | --- |
| A preencher | Backend C#, Oracle e integração Gemini |
| A preencher | Aplicativo Android React Native |

Página de demonstração, aparelho, endereço publicado, evidências de banco e resultados da jornada: **pendentes de confirmação pela equipe**.
