# Plano técnico

## Contexto
Implementa a especificação do encurtador de links, respeitando a constituição
do projeto.

## Arquitetura
- API HTTP em Minimal API, um arquivo de endpoints por recurso
- Camada de aplicação com LinkService, dona das regras RN-01 a RN-06 e de toda
  validação de negócio da entrada
- Camada de dados com EF Core e um repositório por agregado
- Endpoint só desserializa a entrada, chama o serviço e traduz o resultado em
  resposta

## Decisões
- D-01: SQLite como armazenamento, porque o RF-08 exige persistência
  entre reinícios
- D-02: contador incrementado por UPDATE atômico no banco, e não lido e
  reescrito na aplicação, para atender a RN-04 sob acesso concorrente
- D-03: código do link gerado com o alfabeto da RN-05 (A-Z maiúsculo e 1-9,
  35 símbolos) por gerador criptográfico; a exclusão do 0 evita confusão com
  a letra O em link lido em voz alta
- D-04: unicidade do código garantida por índice único no banco, além da
  checagem no serviço, porque a checagem sozinha tem corrida
- D-06: cache em memória (IMemoryCache, já incluso no ASP.NET Core, sem pacote
  novo) só do destino e da expiração por código, para evitar a leitura do link
  no banco a cada redirect do RF-04; o contador continua sempre pelo UPDATE
  atômico da D-02, para não perder acesso do RF-05; a entrada do cache nunca
  vive além de ExpiresAtUtc e a expiração é checada a cada acesso, para
  respeitar a RN-03
- D-07: código automático em colisão (RN-06) é regerado pelo LinkService até
  10 tentativas, tanto quando a checagem acha o código em uso quanto quando a
  gravação viola o índice único da D-04; esgotadas as tentativas, a criação
  falha com erro interno (500), porque com 35^7 combinações isso indica
  defeito, não entrada inválida
- D-08: "publicamente acessível" da RN-02 é verificado só pelo endereço, sem
  requisição nem resolução de DNS: recusa IPs de loopback, privados, link-local,
  CGNAT, multicast e reservados (IPv4 e IPv6), nomes sem domínio (como
  "intranet") e sufixos .localhost, .local, .internal e .localdomain; evita
  dependência de rede nos testes e latência na criação, aceitando que um nome
  público que resolva para IP privado passe
- D-09: a expiração do RF-06 chega como data (expiresOn, yyyy-MM-dd, em UTC) e
  é gravada como ExpiresAtUtc = 00:00 UTC do dia seguinte; o link vale enquanto
  agora < ExpiresAtUtc, o que cobre o fim da data sem depender de 23:59:59;
  expiresOn anterior a hoje em UTC é recusado com 400

## Dependências
- Microsoft.EntityFrameworkCore.Sqlite: provedor do EF Core para SQLite,
  exigido pela constituição e pela D-01
- Microsoft.EntityFrameworkCore.Design: necessário para gerar e aplicar
  migrações com dotnet ef; referenciado com PrivateAssets=all, não vai para
  o runtime
- SQLitePCLRaw.lib.e_sqlite3 2.1.14: fixado explicitamente porque a versão
  2.1.11, trazida de forma transitiva pelo EF Core Sqlite 10.0.10, embute um
  SQLite com vulnerabilidade de gravidade alta (CVE-2025-6965,
  GHSA-2m69-gcr7-jv3q, afeta <= 2.1.11); a 2.1.14 é a última da série 2.x,
  compatível com os demais pacotes SQLitePCLRaw 2.1.x usados pelo EF Core;
  pode ser removido quando o EF Core passar a depender de versão corrigida
- Microsoft.AspNetCore.Mvc.Testing 10.0.10 (só no projeto de testes): sobe a
  API em memória com WebApplicationFactory para os testes de integração que a
  constituição exige para todo endpoint; versão alinhada ao EF Core 10.0.10

## Modelo de dados
Link
- Id, Code (único), TargetUrl, CreatedAtUtc, ExpiresAtUtc (nulo), AccessCount

## Contratos
- POST /api/links entrada: targetUrl, code opcional, expiresOn opcional (data
  yyyy-MM-dd, interpretada em UTC, ver D-09)
- POST /api/links saída: shortUrl, code
- GET /{code} redireciona com 302, ou 404 se não existe, ou 410 se expirou
- GET /api/links/{code}/stats devolve o total de acessos a quem informar o
  código (RF-07), sem autenticação, ou 404 se não existe
- Erros sempre em ProblemDetails, com mensagem em português: validação 400,
  código já em uso 409, código inexistente 404, link expirado 410

## Riscos
- Concorrência na criação de código próprio, mitigada por D-04
- Colisão do código automático, mitigada por D-04 e D-07
- Crescimento da tabela de links sem política de limpeza, fora de escopo por ora