# CitizenTech.Api — guia técnico interno

## 👥 Integrantes

| RM | Nome |
|---|---|
| 557538 | David Cordeiro |
| 555619 | Tiago Morais |
| 557065 | Vinicius Augusto |
| 556892 | Guilherme Lunghini |
| 99856 | Marchel Augusto |

A documentação principal do repositório está no **[README da raiz](../README.md)**: apresentação, versões verificadas, variáveis de ambiente, exemplos dos seis endpoints, migrações, indicadores, testes, equipe e preparação Azure. Use-a como referência operacional; este arquivo concentra detalhes úteis da implementação, sem substituir o contrato.

O [contrato mobile original, revisão 1.1](Especificacao_App_CitizenTech_MVP.md) permanece inalterado. A avaliação e as consultas administrativas são uma expansão separada; health e guidance não dependem de X-Admin-Key.

## Comandos a partir desta pasta

Configure os valores externos conforme a seção de variáveis do README principal. Use o mesmo terminal para iniciar a API; não imprima os segredos.

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet run --launch-profile https
```

Os padrões dos perfis atuais do Visual Studio, definidos em [Properties/launchSettings.json](Properties/launchSettings.json), são **HTTPS `https://localhost:7027`** e HTTP `http://localhost:5200`. O perfil `https`, usado pelo comando e pelos [exemplos HTTP](CitizenTech.Api.http), disponibiliza ambos. O perfil `http` pode disponibilizar somente HTTP; para usá-lo no terminal, execute `dotnet run --launch-profile http` e ajuste a baseUrl para `http://localhost:5200`.

Endereços principais com o perfil HTTPS:

- Health: https://localhost:7027/api/v1/health
- Swagger: https://localhost:7027/swagger
- OpenAPI: https://localhost:7027/openapi/v1.json

Swagger/OpenAPI só são mapeados em **Development**; ambos os perfis atuais definem esse ambiente. Os endereços disponíveis dependem do perfil selecionado no Visual Studio. Usar `--no-launch-profile --urls <ENDERECO>` ignora esses padrões e força um endereço alternativo, fora do fluxo principal.

Para herdar variáveis do PowerShell, inicie a API naquele mesmo terminal. F5 não herda valores definidos no terminal filho; use User Secrets ou configure o ambiente do processo que inicia o Visual Studio. Se necessário, confie no certificado local com `dotnet dev-certs https --trust`; não desabilite validação TLS.

Para compilar/testar toda a solução a partir desta pasta:

```powershell
dotnet build ../CitizenTech.slnx
dotnet test ../CitizenTech.slnx
```

O resultado executado nesta revisão e as limitações das evidências estão na seção **Testes e evidências** do README principal. Não confundir testes HTTP/SQLite/doubles com integração real Oracle, Gemini ou aplicativo.

## Fluxo e pontos de implementação

| Arquivo / pasta | Responsabilidade |
| --- | --- |
| `Program.cs` | DI, contexto Oracle DatabaseVersion19, controllers, OpenAPI e limitadores independentes guidance/admin |
| `Controllers/GuidanceController.cs` | Health e orientação v1 |
| `Services/GuidanceService.cs` | Validar, consultar duplicidade, gerar, validar decisão e aguardar gravação antes de 200 |
| `Services/GuidanceRegistration.cs` | Seleção explícita Fake/Gemini, sem fallback automático |
| `Services/GeminiGuidanceProvider.cs` | Uma chamada HTTP, saída estruturada, timeout e validação; sem retries/redirects |
| `Controllers/AdminController.cs` | Avaliação e três consultas; endpoints protegidos por filtro centralizado |
| `Services/EvaluationService.cs` | Validar existência da sessão, avaliação única e confirmação de persistência antes de 201 |
| `Services/AdminQueryService.cs` | Agregações/projeções LINQ assíncronas, paginação no banco e bases temporais separadas |
| `Errors/AdminKeyFilter.cs` | Header único X-Admin-Key; 401 para chave inválida/ambígua, 503 se configuração ausente |
| `Errors/ContractMiddleware.cs` | Envelope sanitizado, limites do guidance e correlação administrativa gerada pelo servidor |
| `Errors/ContractOpenApiTransformer.cs` | Exemplos e chave administrativa somente nas operações protegidas |
| `Data/` | Mapeamento, gravação, classificação de duplicidade Oracle e migrações |

Guidance reserva orçamento para persistência dentro de 20 s; geração até 15 s e comando Oracle até 5 s. A validação da decisão exige ação permitida e, em highlight, alvo existente/habilitado no snapshot atual. Não manter transação aberta enquanto chama Gemini.

O provider Fake não chama Google, mas **não substitui Oracle**. O padrão de `appsettings.json` é Fake; configurar Gemini explicitamente no ambiente quando necessário. Falta de conexão pode impedir a solicitação antes de alcançar a geração.

## Persistência, erros e concorrência

- `GUIDANCE_RECORDS`: PK em RequestId. O fluxo atual grava somente sucessos, embora a entidade aceite failure. Não anunciar os indicadores como total de tentativas/falhas.
- `SESSION_EVALUATIONS`: PK em Id e índice único em SessionId, sem FK para SessionId não único de guidance. A existência da sessão é validada no serviço.
- Consulta prévia não substitui a restrição única. ORA-00001 é classificado pelo número, não pelo texto; conflito de inserção é traduzido para 409.
- Escrita interrompida pode já ter sido confirmada. Não há retry/replay ou promessa de exatamente uma vez.
- Apenas metadados e avaliação manual são persistidos, sem objetivos, instruções, conteúdo de tela ou credenciais.
- Não habilitar sensitive-data logging, logs de corpos ou captura indiscriminada de exceções internas; os erros públicos usam o envelope v1, sem detalhes internos.

[Detalhes da camada de dados](Data/README.md), [migrações](Data/Migrations) e [scripts SQL](Data/Scripts). Para preparar banco novo ou atualizar somente a partir da inicial, siga as duas rotas separadas no README principal. **Não há aplicação automática no startup; escolha EF OU script após conferir o histórico.** Existência de arquivos não comprova aplicação real.

## Compatibilidade e pendências

Oracle 19c é o destino confirmado pela equipe. O mapeamento usa VARCHAR2 para UUIDs, TIMESTAMP com convenção UTC, NUMBER(2) para nota e NUMBER(1) para booleano numérico. Confirmar materialização e consultas com o provedor Oracle real após aplicação autorizada.

Ainda precisam de evidência separada: tradução/execução LINQ Oracle, migração no esquema alvo, corrida real de unicidade, chamada Google, jornada no aplicativo e acesso às dependências a partir da Azure. Primeiro/último registro são atividade observada, não duração comprovada da tarefa; complete/success não substituem avaliação humana.
