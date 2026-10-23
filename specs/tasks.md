# Tarefas

| ID | Tarefa | Origem | Depende de | Concluída quando |
|---|---|---|---|---|
| T-01 | Criar projeto e estrutura de pastas | Constituição | | Projeto compila |
| T-02 | Modelar a entidade Link e a migração | RF-01, RF-08 | T-01 | Migração aplica no banco |
| T-03 | Gerar código aleatório de 7 caracteres | RF-02, D-03 | T-01 | Teste unitário cobre formato e alfabeto |
| T-04 | Validar URL de destino | RN-02, D-08, borda 2048, borda não pública | T-01 | Teste cobre http, https, inválidos, mais de 2048 caracteres e hosts não públicos da D-08 |
| T-05 | Criar link com código automático | RF-01, RN-06, D-07 | T-02, T-03, T-04 | Teste de integração cria e retorna 201; teste unitário cobre nova tentativa em colisão e esgotamento |
| T-06 | Aceitar código próprio com unicidade | RF-03, RN-01, RN-05, D-04 | T-05 | Teste unitário recusa 0, minúsculas, espaço, caracteres especiais e tamanho fora de 4 a 20 (400 com ProblemDetails); código duplicado retorna 409 |
| T-07 | Redirecionar por código | RF-04, borda código inexistente | T-05 | Teste de integração retorna 302 para link existente e 404 com ProblemDetails para código inexistente |
| T-08 | Incrementar contador de forma atômica | RF-05, RN-04, D-02 | T-07 | Teste concorrente não perde contagem |
| T-09 | Endpoint de estatísticas público | RF-07 | T-05, T-12 | Teste de integração consulta pelo código e recebe o total de acessos com 200; código inexistente retorna 404 |
| T-10 | Cache em memória do destino por código | D-06, RF-04 | T-08, T-11 | Segundo redirect do mesmo código não lê o link no banco; contador incrementa em todos os acessos; link expirado em cache retorna 410 sem contar acesso |
| T-11 | Expirar link por data em UTC | RF-06, RN-03, D-09, borda data passada | T-05, T-08 | Data passada retorna 400; link vale até o fim do dia UTC e depois retorna 410 sem contar acesso |
| T-12 | Remover chave de gerenciamento do código | RF-07 | T-05 | Entidade, migração, serviço, resposta do POST e testes sem ManagementKey; testes passam |