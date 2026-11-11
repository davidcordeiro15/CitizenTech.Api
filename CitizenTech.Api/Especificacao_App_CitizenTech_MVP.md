# CitizenTech — instruções para implementar o app e integrar com a API

Contrato HTTP: v1. Revisão do documento: 1.1, em 07/11/2026.

Revisão 1.1: Oracle obrigatório pela API e documentação de Mobile. Requisição e resposta de sucesso continuam com contractVersion "1"; foram acrescentados erros de persistência e requisição já registrada. Telas, navegação e demais requisitos funcionais mantêm o escopo anterior.

Este documento é a especificação de implementação, não uma descrição de código já existente. O endereço definitivo da API, o aparelho e o site de demonstração ainda serão informados pela equipe. Os exemplos de página e botões são fictícios. Não invente esses dados nem declare testes que não executou.

## 1. Leia primeiro: o que estamos construindo

Crie um aplicativo Android que ajude idosos e pessoas com dificuldade em tecnologia a usar uma página no Chrome. O usuário pede ajuda, o assistente lê os elementos disponíveis, fala uma instrução e desenha um destaque sobre o elemento correto. O próprio usuário toca e preenche a página.

Exemplo: a pessoa diz “Quero agendar a renovação do meu RG”. Na página de demonstração, o assistente circula “Agendamento” e fala “Toque no botão destacado”. Depois do toque, a pessoa pede a próxima orientação.

O assistente deve aparecer POR CIMA DO CHROME, como o controle flutuante de um gravador de tela. Não colocar a página dentro de uma WebView. Não criar um navegador próprio. Não substituir a página por uma imagem.

Objetivo: demonstrar uma jornada curta funcionando em um aparelho Android escolhido. Não criar um assistente universal.

## 2. Divisão de responsabilidades

| Responsável | Entrega |
| --- | --- |
| Desenvolvedor do app | React Native, módulo Android, permissões, leitura da tela, destaques, voz, cliente HTTP, simulação e APK |
| David — backend | API C#, integração Gemini, persistência Oracle com EF Core e migrações, validação das respostas, tratamento de erros e publicação Azure |
| Ambos | Seguir exatamente o contrato da seção 7, testar a jornada no mesmo aparelho/site e registrar as evidências de Mobile |

O app NÃO chama Gemini diretamente. A chave da IA fica exclusivamente no servidor. O backend NÃO desenha o círculo nem controla os toques. A hospedagem é responsabilidade do backend.

O banco é o Oracle em nuvem da faculdade, com acesso de qualquer rede informado pela equipe. Somente a API acessa o banco. Não instalar driver Oracle no app nem adicionar Firebase ou banco local. Host, porta, serviço, usuário e senha serão configurados por David fora do repositório. Antes da apresentação, testar a conexão a partir da API na Azure; não presumir que acesso pelo computador comprova acesso do ambiente hospedado.

## 3. Escopo obrigatório e limites

- Android, um celular de teste e Chrome.
- React Native com TypeScript e Expo development build; não depender de Expo Go.
- Um módulo Expo local Android, com Kotlin apenas onde o sistema exigir código nativo.
- Uma tela inicial com “Iniciar ajuda”, “Encerrar ajuda” e estado das permissões.
- Capturar o objetivo antes de sair da tela inicial; permitir falar e, como alternativa, digitar.
- Sobre o Chrome, controle compacto com “Próximo passo” e “Encerrar”. Não adicionar chat completo.
- Uma orientação por solicitação. Não iniciar novas chamadas automaticamente a cada evento da tela.
- Resposta em texto curto e voz em português do Brasil.
- Um destaque por vez, sem bloquear os toques no elemento abaixo.
- Remover destaque ao mudar/rolar a página, ao encerrar e em caso de resposta inválida.
- Sem login, cadastro, perfil, histórico visual, painel administrativo, pagamentos, banco local, ativação por palavra, iOS ou cliques automáticos.
- Sem captura de tela/OCR no primeiro ciclo. Se a árvore de acessibilidade não servir para o site, registrar o bloqueio e testar uma página demonstrativa identificada. Não trocar a estratégia silenciosamente.

## 4. Arquitetura mínima

Use uma aplicação React Native e um módulo Expo local para a integração Android. Não criar uma arquitetura de dezenas de camadas ou vários serviços.

Interface React Native: tela inicial, configurações de desenvolvimento, permissões e início/fim da sessão.

Módulo Android: AccessibilityService, árvore da janela, coordenadas locais, overlay, TextToSpeech e ciclo de orientação enquanto o Chrome está aberto. A execução da requisição HTTP deve pertencer a esse ciclo nativo, usando um cliente Android simples. Não depender de um timer JavaScript ou da Activity React Native permanecer visível. A interface pode apenas espelhar os estados nativos por eventos.

Preferir overlay de acessibilidade ligado ao serviço. Não pedir automaticamente SYSTEM_ALERT_WINDOW se a implementação escolhida não precisar dessa permissão. Não ativar touch exploration. O serviço acompanha somente os pacotes necessários para o cenário de teste.

Manter o módulo local e suas configurações de manifesto de maneira reproduzível no projeto Expo. Documentar como recompilar depois de alterações nativas. Não depender de edições manuais que se perdem ao regenerar o projeto.

Não é requisito continuar depois de fechar à força o app, reiniciar ou bloquear o aparelho. Ao bloquear/encerrar a sessão, suspender a ajuda e limpar os recursos. O contexto ativo da sessão não precisa sobreviver a reinícios; os registros técnicos já confirmados no Oracle devem permanecer gravados.

## 5. Jornada de uso

1. Abrir o app e explicar em uma frase que ele lê elementos da tela durante a ajuda.
2. Exibir permissões pendentes. O usuário habilita o serviço de acessibilidade nas configurações; o app não pode habilitá-lo sozinho.
3. Solicitar microfone apenas para fala. Se negado/indisponível, manter entrada por texto.
4. Registrar o objetivo e iniciar uma sessão local com UUID.
5. O usuário abre a página de teste no Chrome. Não implementar descoberta de serviços na internet. Opcionalmente abrir um endereço previamente configurado com navegador externo; não presumir que abrir URL genérica garante Chrome.
6. Mostrar “Próximo passo” e “Encerrar” sobre o Chrome.
7. Ao pedir o próximo passo: apagar destaque anterior, aguardar a página estabilizar, coletar snapshot, chamar API, validar resposta, destacar e falar.
8. A pessoa realiza a ação na página e solicita o próximo passo. Nunca clicar ou preencher por ela.
9. Se a API pedir esclarecimento, oferecer uma entrada curta de texto/fala em Activity do assistente. Preservar a sessão e esperar a pessoa voltar ao Chrome antes de coletar o próximo snapshot. Não enviar a tela dessa Activity como se fosse a página.
10. “Encerrar” cancela chamadas, para voz, remove overlays e limpa dados da sessão. O serviço pode continuar habilitado nas configurações, mas deve ficar inativo sem coletar conteúdo.

Para diminuir complexidade, não exigir gravação de voz com Chrome em primeiro plano. O objetivo pode ser capturado na Activity do app antes da sessão. Não implementar escuta permanente. Se adicionar microfone sobre outro app, isso é uma extensão a validar com as regras de foreground service do Android, não uma suposição do MVP.

## 6. Leitura da tela e posicionamento

- Usar a janela de conteúdo do Chrome. Ignorar controles do assistente e, quando possível, barra de endereço e interface do navegador.
- Coletar textos relevantes e controles visíveis, com IDs temporários. Não enviar dump completo da árvore.
- Priorizar botões, links, rótulos e campos. Descartar nós invisíveis, sem área útil ou repetidos.
- Não enviar senhas nem valores digitados em formulários. Para campos, enviar rótulo/tipo e nunca seu conteúdo. Usar apenas contas e dados fictícios na demonstração; não registrar dumps ou conteúdo de tela em logs.
- Se não for possível distinguir rótulo de valor sensível, omitir o texto desse nó.
- No máximo 80 elementos, até 160 caracteres por texto e 64 KiB por requisição. Priorizar interativos e seus rótulos quando exceder.
- Para cada leitura, criar snapshotId UUID e IDs e1, e2 etc. válidos apenas naquele snapshot. IDs não precisam ser persistentes entre páginas.
- Guardar localmente o mapa elementId -> retângulo em pixels da tela. Não enviar coordenadas para a API.
- Usar os limites retornados pelo sistema e converter conforme o espaço de coordenadas do overlay. Não usar posições fixas, porcentagens inventadas ou coordenadas fornecidas pela IA.
- Ao mudar conteúdo, janela, orientação, rolagem ou teclado, invalidar a leitura e retirar destaque. Filtrar eventos do próprio overlay para não invalidar tudo por causa dele.
- Antes de capturar, aguardar aproximadamente 500 ms sem mudança relevante, até um máximo de 3 s. Se continuar instável, pedir para tentar novamente em vez de apontar.
- Antes de aplicar resposta, conferir sessão ativa, requestId, snapshotId e ausência de mudança relevante desde a coleta. Se mudou, descartar sem falar a instrução antiga; permitir nova tentativa manual.
- Nenhum elemento legível: informar “Não consegui ler esta página. Tente novamente.” e não chamar IA com uma tela vazia.

## 7. Contrato HTTP v1 — fonte comum para app e backend

Esta seção deve ser seguida por ambos. Não alterar nomes, tipos ou enumerações de um lado apenas. Qualquer alteração precisa atualizar este arquivo e os dois projetos.

### Configuração

- baseUrl é a raiz do servidor, SEM /api/v1. Exemplo fictício: https://SEU-APP.azurewebsites.net.
- Compor os caminhos abaixo sem duplicar barras ou prefixos.
- Configurar baseUrl e modo mock/real em configuração de desenvolvimento; não criar uma tela de configurações para o usuário final.
- Copiar essa configuração ao módulo nativo quando a sessão iniciar.
- Em produção/demonstração publicada, HTTPS. HTTP pode ser permitido somente em debug para o host local especificado, sem desabilitar validação TLS.
- Celular físico usa IP local do computador na mesma rede; localhost no celular aponta para o próprio celular. O backend local precisa escutar na interface acessível e o firewall permitir a porta.
- Sem autenticação de usuário nesta versão. Não colocar chave Gemini, credencial Azure ou segredo compartilhado no APK. David deve limitar corpo, taxa de chamadas e tempo de execução da API publicada e desligar o ambiente após a demonstração quando não estiver em uso.
- Aplicativo nativo não depende de CORS para chamadas HTTP. Configuração CORS não resolve conexão bloqueada por rede/firewall.

### GET /api/v1/health

HTTP 200, Content-Type application/json:

```json
{"status":"ok","contractVersion":"1"}
```

Esse endpoint verifica disponibilidade/versão da aplicação, sem chamar Gemini nem consultar Oracle. HTTP 200 no health não garante disponibilidade das dependências; somente um POST concluído comprova o ciclo de orientação e gravação. Pode ser usado no diagnóstico de desenvolvimento; não bloquear a interface com polling.

### POST /api/v1/guidance

Headers: Content-Type: application/json; Accept: application/json.

JSON em UTF-8, nomes camelCase, UUIDs em string, enumerações minúsculas. Campos abaixo são obrigatórios; os explicitamente nullable aceitam null. Arrays vazios permitidos conforme regra. O servidor pode acrescentar campos no futuro sem quebrar clientes que os ignoram.

Requisição completa de exemplo:

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

| Campo | Regra |
| --- | --- |
| contractVersion | String fixa "1" |
| requestId | UUID novo a cada solicitação |
| sessionId | UUID criado pelo app ao iniciar ajuda; apenas correlação, não login |
| locale | String fixa "pt-BR" |
| goal | Objetivo da sessão; 1 a 500 caracteres |
| userMessage | Resposta a esclarecimento, até 500 caracteres, ou null; não enviar dados pessoais |
| history | Últimas 3 orientações válidas, da mais antiga para a mais nova; [] no início |
| history[].action | Uma das ações definidas abaixo |
| history[].instruction | Texto da orientação anterior, até 250 caracteres; não implica ação concluída |
| screen.snapshotId | UUID da leitura atual |
| screen.packageName | Pacote da janela de conteúdo capturada |
| screen.elements | De 1 a 80 elementos |
| elements[].id | Único dentro do snapshot; 1 a 40 caracteres |
| elements[].role | button, link, input, text, scroll ou other |
| elements[].text | Rótulo/texto visível, até 160 caracteres; pode ser vazio, nunca valor pessoal de formulário |
| elements[].enabled | Boolean; não escolher controle desabilitado como alvo |

Exemplo de history no segundo pedido: [{"action":"highlight","instruction":"Toque em Agendamento, no botão destacado."}]. A tela atual é a evidência do estado; o histórico não comprova que o usuário executou a instrução.

Não mandar áudio, screenshot, HTML, coordenadas, árvore bruta ou URL com parâmetros pessoais neste contrato.

### Resposta HTTP 200

```json
{
  "contractVersion": "1",
  "requestId": "a1111111-1111-4111-8111-111111111111",
  "sessionId": "b2222222-2222-4222-8222-222222222222",
  "snapshotId": "c3333333-3333-4333-8333-333333333333",
  "action": "highlight",
  "instruction": "Toque em Agendamento, no botão destacado.",
  "targetElementId": "e1"
}
```

Todos os campos são obrigatórios. requestId/sessionId/snapshotId devem ser exatamente os da requisição. instruction: 1 a 250 caracteres em português, uma orientação por vez, tom respeitoso. Usar o MESMO texto no overlay e no TextToSpeech. targetElementId é string ou null conforme a ação.

No modo real, HTTP 200 só pode ser devolvido depois de validar a orientação e confirmar a gravação dos metadados no Oracle. Vale para highlight, explain, ask e complete. O app não faz chamada adicional para salvar e não precisa receber ID de banco nem flag saved: a confirmação de gravação faz parte da garantia de HTTP 200. Essa garantia é de registro da orientação gerada, não de que o usuário a viu, tocou no alvo ou concluiu o serviço.

| action | targetElementId | Comportamento do app |
| --- | --- | --- |
| highlight | ID de elemento habilitado do snapshot atual | Destacar retângulo local e falar |
| explain | null | Mostrar/falar instrução sem destaque, por exemplo pedir rolagem manual |
| ask | null | Falar pergunta; permitir resposta em Activity do assistente e novo pedido após voltar ao Chrome |
| complete | null | Mostrar/falar conclusão e limpar o destaque; usuário pode encerrar |

Não adicionar ação de clique, preenchimento, abertura de URL, execução de código ou script remoto. Rolagem é feita pela pessoa, orientada por explain. complete exige evidência na página ou confirmação explícita coerente; não anunciar agendamento real em página simulada.

Resposta com ID inexistente, versão incompatível, enum desconhecido ou formato inválido não deve produzir destaque nem leitura da orientação. Mostrar mensagem local de falha e permitir tentar novamente.

### Erros HTTP

Formato comum, em qualquer erro tratado pelo backend:

```json
{
  "contractVersion": "1",
  "requestId": "a1111111-1111-4111-8111-111111111111",
  "error": {
    "code": "AI_UNAVAILABLE",
    "message": "Não consegui orientar agora. Tente novamente."
  }
}
```

requestId pode ser null se o servidor não conseguiu interpretar a requisição. Nunca devolver stack trace, chave ou resposta bruta do provedor.

| HTTP | code | Quando usar |
| --- | --- | --- |
| 400 | INVALID_REQUEST | Campos ou versão inválidos |
| 409 | REQUEST_ALREADY_RECORDED | requestId já existe no Oracle; não chamar novamente a IA |
| 413 | REQUEST_TOO_LARGE | Corpo acima de 64 KiB |
| 429 | RATE_LIMITED | Limite de chamadas; Retry-After opcional |
| 502 | AI_UNAVAILABLE | Provedor falhou ou resposta da IA inválida |
| 503 | PERSISTENCE_UNAVAILABLE | Não foi possível confirmar gravação no Oracle, inclusive timeout de banco |
| 504 | AI_TIMEOUT | Tempo da IA excedido |
| 500 | INTERNAL_ERROR | Erro interno genérico |

Exemplo de falha de persistência, HTTP 503, usando o mesmo envelope de erro:

```json
{
  "contractVersion": "1",
  "requestId": "a1111111-1111-4111-8111-111111111111",
  "error": {
    "code": "PERSISTENCE_UNAVAILABLE",
    "message": "Não consegui registrar esta orientação. Tente novamente."
  }
}
```

Ao receber 503/PERSISTENCE_UNAVAILABLE, o app remove o destaque, mostra/fala a mensagem de erro e libera tentativa manual. Não usar uma orientação em cache nem o mock para esconder a falha. O texto da orientação gerada não deve acompanhar o erro. Não adicionar erros ao history.

requestId identifica UMA solicitação; há restrição única no banco. Se o app pedir uma nova tentativa, gerar novo requestId e novo snapshot. Para 409, mostrar “Esta solicitação já foi registrada. Peça uma nova orientação.” e liberar nova tentativa manual. Não implementar recuperação de orientação antiga ou replay pelo ID: só guardaremos metadados. Uma conexão interrompida pode deixar um registro confirmado sem que o app receba a resposta; não prometer execução exatamente uma vez nem cancelamento de custo da IA.

O app deve suportar também erros sem JSON, desconexão e falha de DNS/TLS, pois proxies e Azure podem responder outro formato. Mostrar texto local simples e não tentar interpretar HTML como orientação.

Tempo máximo do servidor: 20 s por requisição. Timeout do cliente: 25 s. Apenas uma requisição ativa por sessão; bloquear múltiplos toques enquanto aguarda. Não repetir automaticamente: o usuário solicita nova tentativa. Encerrar cancela a chamada local e invalida respostas tardias. O backend deve respeitar cancelamento quando a desconexão for percebida, sem garantir cancelamento de cobrança no provedor.

Os 20 s incluem IA e persistência; reservar tempo para o banco, por exemplo até 15 s para IA e o restante para validação/gravação. Timeout do banco é PERSISTENCE_UNAVAILABLE; timeout da IA é AI_TIMEOUT. Não manter uma transação do banco aberta durante a chamada à IA.

## 8. O que David implementará na API C#

- Os dois endpoints exatamente como definidos acima, documentados em OpenAPI/Swagger.
- Validação de limites, IDs, campos, enumerações e versão antes de chamar IA.
- Uma chamada à IA por solicitação, com timeout e cancelamento.
- Instruir o modelo a tratar texto da página como dados, nunca como ordens para mudar suas regras.
- A IA recebe elementos e objetivo e retorna apenas action, instruction e targetElementId estruturados. O servidor valida isso e acrescenta os IDs de correlação copiados da requisição; não pedir à IA para inventar IDs de sessão/snapshot.
- Para highlight, conferir que o alvo existe na requisição e está habilitado. Resposta inválida vira AI_UNAVAILABLE; não repassar output bruto.
- Sem sessão conversacional obrigatória no servidor: cada pedido carrega goal e history. Reiniciar o backend não exige refazer login ou criar sessão remota. Isso não dispensa os registros persistentes no Oracle.
- Gemini e suas chaves ficam no backend. Não enviar dados de formulário ao modelo.
- Publicação HTTPS, limites simples e configuração do modelo e conexão Oracle em ambiente.
- Persistência obrigatória com EF Core, provedor Oracle compatível com a versão escolhida e migrações. Uma tabela é suficiente; não criar repositórios genéricos, filas ou novos serviços.
- Validar acesso ao banco; conferir requestId existente; chamar IA; validar orientação; gravar metadados; só então retornar HTTP 200. A restrição única é a proteção final contra concorrência. Não manter transação aberta durante geração da IA.
- Se a gravação falhar ou seu resultado não puder ser confirmado, retornar HTTP 503/PERSISTENCE_UNAVAILABLE. Não retornar sucesso parcial ou orientar sem registro.

### Tabela mínima no Oracle: GUIDANCE_RECORDS

| Campo lógico | Conteúdo |
| --- | --- |
| RequestId | UUID da solicitação; chave primária/única, pode ser armazenado como texto de 36 caracteres |
| SessionId | UUID de correlação da sessão, sem criar usuário ou tabela de sessão |
| CreatedAtUtc | Data/hora UTC definida pelo servidor |
| Action | highlight, explain, ask ou complete; null para falha anterior a uma orientação válida |
| Result | success ou failure; success significa orientação válida com gravação confirmada, não conclusão da tarefa |
| ErrorCode | Código técnico de falha ou null em sucesso |

Não armazenar goal, userMessage, history, instruction, textos da página, elementos, screenshots, dados preenchidos ou credenciais nessa tabela. A API usa o conteúdo recebido apenas durante o processamento.

Falhas de IA podem ser registradas como failure quando o Oracle estiver disponível. Se o próprio banco estiver indisponível, não é possível garantir registro da falha nele: usar log técnico sanitizado, sem conteúdo da tela ou dados pessoais. Não criar outro banco ou fila para esse caso. Falha ao salvar uma orientação válida deve obrigatoriamente impedir HTTP 200.

O backend pode ser desenvolvido com o JSON de exemplo enquanto o amigo implementa o app. O app pode ser desenvolvido com mock enquanto David implementa a API.

## 9. Modo simulado obrigatório

Criar um mock no MESMO caminho de execução usado pelo cliente HTTP nativo, selecionado por configuração. Ele recebe uma requisição real montada a partir da tela, espera aproximadamente 500 ms e devolve exatamente o formato v1.

Comportamento mínimo: se houver elemento habilitado com texto “Agendamento”, devolver highlight para o ID real dele. Caso contrário, devolver explain com “Esta é uma resposta de teste. Abra a página de demonstração.”. Copiar os IDs da requisição. Nunca devolver e1 fixo sem verificar o snapshot.

O mock testa coleta, destaque e funcionamento sobre o Chrome. Não prova integração com Gemini. Identificar claramente o modo no diagnóstico/README e nunca apresentar resposta simulada como IA real.

O mock local não grava Oracle e não comprova o critério de banco de dados da entrega Mobile. As evidências finais de persistência devem usar o endpoint real e mostrar um registro confirmado. Uma implementação temporária HTTP com IA simulada pode testar a persistência se realmente gravar no Oracle, desde que identificada como IA simulada. Não mudar automaticamente do modo real para mock após um erro.

Para conferir o contrato por HTTP, David pode começar com uma implementação temporária de POST que aplique essa mesma regra, substituindo depois apenas o serviço de IA. Não mudar o JSON nessa troca.

## 10. Ordem de implementação e checkpoints

1. Criar projeto e instalar development build no aparelho; registrar versões do SDK Expo, Android e Chrome.
2. Criar módulo/serviço Android, ativar acessibilidade e desenhar um retângulo sobre Chrome sem impedir toque.
3. Ler botão real e desenhar pelo retângulo retornado pelo Android. Testar rolagem e remover destaque antigo.
4. Implementar sessão e mock do contrato; provar ciclo completo enquanto React Native está em segundo plano.
5. Integrar endpoint real com a mesma requisição/resposta, gravação Oracle e tratamento de 503/409. Ainda pode usar objetivo digitado.
6. Adicionar fala inicial e TextToSpeech. Falha de voz não pode impedir orientação visual.
7. Ensaiar cinco etapas na página escolhida. Preparar APK com bundle JavaScript embarcado, sem depender do Metro, e repetir o teste nesse APK.

Não começar com login, design elaborado ou chat. Se leitura/overlay não funcionar, registrar evidência e resolver antes de ampliar o restante. Se trocar de site, testar novamente; não afirmar compatibilidade universal.

## 11. Critérios de aceite

- [ ] Instala e abre no aparelho Android definido.
- [ ] Permissões negadas geram orientação compreensível, sem loop ou travamento.
- [ ] Após o usuário iniciar ajuda, o controle aparece sobre Chrome.
- [ ] A chamada real e o destaque funcionam com a Activity React Native em segundo plano.
- [ ] JSON enviado e recebido segue a seção 7 sem adaptações escondidas.
- [ ] O elemento real é destacado pelo mapa local; não há toque automático.
- [ ] Rolar/navegar/abrir teclado limpa indicação antiga; resposta atrasada não aponta para outro lugar.
- [ ] IDs inválidos e tela ilegível não geram destaque inventado.
- [ ] Falha de internet, timeout, 429 e resposta não JSON exibem falha simples e liberam nova tentativa.
- [ ] Apenas uma chamada é feita por toque aceito.
- [ ] Encerrar remove todos os overlays e interrompe atividade da sessão.
- [ ] Voz em pt-BR ou fallback visual funcional se o mecanismo de voz não estiver disponível.
- [ ] Sem chaves ou dados pessoais em APK, requisições de exemplo ou logs.
- [ ] A demonstração funciona em APK independente do Metro.
- [ ] Um POST real bem-sucedido produz registro no Oracle com o mesmo requestId/sessionId.
- [ ] Falha de persistência retorna 503, não fala orientação gerada nem mostra destaque e permite nova tentativa manual.
- [ ] A evidência de banco diferencia orientação registrada de tarefa efetivamente concluída pelo cidadão.
- [ ] README e vídeo de Mobile entregues conforme seção 12.

Testar no aparelho real. Simulador, preview web ou um círculo dentro da própria tela do app não comprovam sobreposição sobre Chrome. Informar explicitamente o que foi e não foi testado. Automatizar apenas validações úteis do contrato/estado; não criar uma infraestrutura extensa de testes.

## 12. Entrega do desenvolvedor do app

Entregar repositório com app e módulo nativo, configuração de exemplo sem segredos, modo mock, instruções para build e instalação, APK de demonstração, versões usadas e limitações conhecidas. Incluir um exemplo de requisição real sanitizada e o resultado do teste com a API de David.

### Documentação obrigatória para Mobile Development & IOT

O repositório GitHub do app deve conter README com:

1. Ficha da CitizenTech: nome do projeto, cidade/bairro ou território, cidadão beneficiado, problema em uma frase, funcionamento, papel de cada parte, link do repositório, indicador de impacto e ODS relacionados. Preencher somente informações confirmadas; resultados de validação ainda não realizados devem ser identificados como pendentes.
2. Integrantes e responsabilidades; link do repositório da API.
3. Tecnologias, versões e aparelho/Android/Chrome usados nos testes.
4. Passos para instalar dependências, gerar development build, executar e instalar o APK final, incluindo configuração da baseUrl e permissões. Explicar que Expo Go não executa a integração nativa.
5. Descrição do contrato v1 e do fluxo: app -> API C# -> Gemini/Oracle -> app. Explicar que a API salva metadados antes de responder 200; o app não usa Firebase nem acessa Oracle diretamente.
6. Prints das telas do app e do overlay sobre o Chrome, usando somente dados fictícios. Não incluir credenciais ou dados pessoais.
7. Evidência de persistência: identificar requestId/sessionId de teste e mostrar registro correspondente no Oracle por captura ou consulta sanitizada fornecida por David. A documentação deve permitir relacionar o uso do app à gravação. Não criar uma tela de histórico nem um endpoint público para consultar registros só para obter essa evidência.
8. Evidência do erro 503 e recuperação por nova tentativa manual, em ambiente de teste controlado. Pode ser print complementar ao vídeo.
9. Limitações conhecidas e indicação explícita do que está em mock, API real ou página demonstrativa.
10. Link de vídeo de até 3 minutos com o app rodando.

Sugestão para o vídeo: explicar o problema/público, mostrar pedido de ajuda, destacar/falar uma orientação sobre Chrome e mostrar uma evidência rápida da gravação correspondente no Oracle. Não expor conexão, senha ou dados pessoais. O vídeo de Mobile de até 3 minutos é diferente do pitch geral da GS.

Conforme o briefing fornecido, um integrante envia o link do repositório no Teams até 19/11/2026 às 23h55, horário local da faculdade. Conferir a tarefa da disciplina para eventuais instruções complementares. A documentação não substitui a comprovação de funcionamento. Nesta revisão, não acrescentar telas, rotas ou requisitos de acessibilidade além do escopo anterior.

Antes de integrar, ambos confirmam: baseUrl, contrato v1, aparelho/Android/Chrome, página de demonstração e status mock/real. Nenhum desses dados exige criar telas extras no produto.

## 13. Referências técnicas

- Expo development builds: https://docs.expo.dev/develop/development-builds/introduction/
- Módulos nativos locais: https://docs.expo.dev/workflow/customizing/
- AccessibilityService, árvore e overlays: https://developer.android.com/reference/android/accessibilityservice/AccessibilityService
- Restrições de serviços/microfone em segundo plano: https://developer.android.com/develop/background-work/services/fgs/restrictions-bg-start

Essas referências explicam capacidades e restrições da plataforma. O contrato HTTP e os recortes deste arquivo são decisões propostas para este projeto, não requisitos dessas documentações.
