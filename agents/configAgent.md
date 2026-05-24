Você é um arquiteto/backend engineer especialista em .NET, qualidade de software, testes automatizados, Git hooks, CI/CD e boas práticas de engenharia.

Estou criando uma POC em .NET 10 com foco em arquitetura, mensageria, observabilidade, outbox/inbox pattern, Kafka, PostgreSQL e boas práticas profissionais de desenvolvimento.

Quero que você implemente uma estrutura de Quality Gate local usando Lefthook, .editorconfig, Directory.Build.props, testes automatizados e cobertura mínima para código novo.

Objetivo principal:
Garantir que todo commit e push passe por validações automáticas de qualidade, incluindo estilo de código, build, testes unitários, testes integrados e cobertura mínima de 80% para código novo.

Requisitos gerais:

1. Usar Lefthook como gerenciador de Git hooks.

2. Criar ou ajustar o arquivo lefthook.yml na raiz do projeto.

3. Criar ou ajustar o arquivo .editorconfig na raiz do projeto para padronizar estilo de código C#.

4. Criar ou ajustar o arquivo Directory.Build.props na raiz do projeto para centralizar configurações comuns de build.

5. Criar scripts dentro de uma pasta tools/quality para validações auxiliares.

6. Garantir que novas classes relevantes tenham testes correspondentes.

7. Garantir cobertura mínima de 80% para código novo.

8. Separar corretamente as validações entre pre-commit e pre-push para não deixar o fluxo de commit lento demais.

Estrutura desejada:

/
  .editorconfig
  Directory.Build.props
  lefthook.yml
  src/
  tests/
    UnitTests/
    IntegrationTests/
  tools/
    quality/
      validate-new-classes-have-tests.sh
      run-unit-tests-with-coverage.sh
      run-integration-tests-with-coverage.sh

Regras para pre-commit:

O pre-commit deve executar validações rápidas:

- dotnet format --verify-no-changes
- dotnet build
- validação de classes novas com testes correspondentes

Não rodar testes integrados pesados no pre-commit.

Regras para pre-push:

O pre-push deve executar validações mais completas:

- dotnet test para testes unitários
- dotnet test para testes integrados
- coleta de code coverage
- validação de cobertura mínima de 80%
- validação de que código novo relevante possui testes

Cobertura de testes:

Quero cobertura mínima de 80% para código novo.

Essa cobertura deve ser validada tanto para:

- testes unitários
- testes integrados

Regras esperadas:

- Código novo em Application, Domain e Infrastructure deve ter teste correspondente quando fizer sentido.
- Handlers devem ter testes unitários.
- Services devem ter testes unitários.
- Strategies devem ter testes unitários.
- Validators devem ter testes unitários.
- Repositories devem ter testes integrados.
- Consumers devem ter testes integrados ou testes unitários com dependências mockadas.
- Outbox/Inbox processors devem ter testes de integração quando envolverem banco de dados.
- Código de configuração simples, DTOs, Requests, Responses, Options, Program.cs e migrations podem ser ignorados da regra de teste correspondente.

Estilo de código:

Criar um .editorconfig com padrões adequados para C# moderno:

- indentação com 4 espaços
- final de linha consistente
- ordenar usings
- preferir var quando o tipo for óbvio
- evitar this desnecessário
- habilitar severidades de analyzers importantes
- tratar regras relevantes como warning ou error
- manter padrão consistente para namespaces, usings e estilo de código

Directory.Build.props:

Criar um Directory.Build.props com configurações globais de qualidade:

- Nullable enable
- ImplicitUsings enable
- AnalysisLevel latest
- EnforceCodeStyleInBuild true
- TreatWarningsAsErrors true

Não colocar TargetFramework no Directory.Build.props se isso puder atrapalhar projetos diferentes. Se fizer sentido para a solução inteira, explicar a decisão antes.

Lefthook:

Criar um lefthook.yml com pelo menos:

pre-commit:
- format
- build
- validate-new-classes-have-tests

pre-push:
- unit-tests
- integration-tests
- coverage

Scripts:

Criar scripts simples, claros e comentados em Bash.

Os scripts devem:

1. validate-new-classes-have-tests.sh

- Buscar arquivos novos usando git diff.
- Verificar arquivos .cs adicionados em src/.
- Ignorar Program.cs, DTOs, Requests, Responses, Options, Migrations, Constants e arquivos de configuração simples.
- Para cada classe nova relevante, procurar um arquivo de teste correspondente com o padrão NomeDaClasseTests.cs.
- Falhar com mensagem clara caso algum teste esteja faltando.

2. run-unit-tests-with-coverage.sh

- Rodar testes unitários.
- Coletar cobertura.
- Falhar caso a cobertura fique abaixo de 80%.
- Gerar relatório em uma pasta previsível, como TestResults/Coverage/Unit.

3. run-integration-tests-with-coverage.sh

- Rodar testes integrados.
- Coletar cobertura.
- Falhar caso a cobertura fique abaixo de 80%.
- Gerar relatório em uma pasta previsível, como TestResults/Coverage/Integration.

Ferramentas de cobertura:

Usar uma abordagem compatível com .NET moderno, preferencialmente com:

- coverlet
- XPlat Code Coverage
- ReportGenerator, se necessário

Caso seja necessário adicionar pacotes NuGet aos projetos de teste, sugerir os pacotes e comandos.

Exemplos de pacotes esperados:

- coverlet.collector
- Microsoft.NET.Test.Sdk
- xunit
- xunit.runner.visualstudio
- FluentAssertions
- Testcontainers, para testes integrados

Critérios de aceite:

Ao final, eu quero conseguir executar:

lefthook install

E depois:

git commit

O commit deve falhar se:

- o código não estiver formatado
- o build falhar
- uma classe nova relevante não tiver teste correspondente

Ao executar:

git push

O push deve falhar se:

- testes unitários falharem
- testes integrados falharem
- cobertura de código novo ficar abaixo de 80%
- alguma validação de qualidade falhar

Importante:

- Explique brevemente cada arquivo criado.
- Comente os scripts, pois esta é uma POC de estudo.
- Evite soluções complexas demais no primeiro momento.
- Priorize uma solução funcional, evolutiva e fácil de entender.
- Não implemente Roslyn Analyzer customizado agora.
- Não crie dependência de ferramenta paga.
- Não ignore falhas silenciosamente.
- Mensagens de erro devem ser claras e educativas.
- Mantenha compatibilidade com ambiente Linux/WSL.

Antes de implementar, apresente um plano curto com:

1. arquivos que serão criados ou alterados;
2. comandos necessários;
3. pacotes NuGet necessários;
4. limitações da abordagem inicial.

Depois implemente os arquivos.