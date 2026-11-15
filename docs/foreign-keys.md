# Chaves estrangeiras adicionadas em 2026-11-15

A migração `AddMissingForeignKeys` cria 31 chaves estrangeiras que faltavam entre as tabelas centrais: trabalhador, unidade, setor, cargo, equipe, usuário, perfil, permissão, sessão e documento.

- **Não destrutiva:** nenhuma linha é alterada ou removida.
- **`NOT VALID`:** as chaves passam a valer para toda nova gravação, mas a migração não falha se um banco existente já tiver registros órfãos.
- **`ON DELETE RESTRICT`:** combina com o código, que só faz exclusão lógica.
- **Relações também declaradas no modelo do EF:** assim o Entity Framework grava o registro pai antes do filho quando os dois são salvos juntos. Sem isso, as constraints recusariam a gravação.
- **Ficam sem chave estrangeira, de propósito:**
  - colunas de autoria (`criado_por`, `atualizado_por`, `excluido_por`, `ator_id`);
  - a tabela de auditoria;
  - referências polimórficas ou de idempotência (`entidade_id`, `referencia_id`, `comando_id`, `evento_origem_id`).

## Depois de aplicar em cada ambiente

1. Conte os órfãos com o SQL abaixo. Ele só faz leitura.
2. Se todas as contagens forem zero, valide as chaves (segundo bloco). A validação lê as tabelas e não bloqueia gravações.
3. Se houver órfãos, **não apague nada sem decisão da equipe**. Corrigir ou remover esses registros muda dados e precisa de aprovação. Enquanto isso, as chaves continuam protegendo as gravações novas.

Ensaio: em 15/11/2026, numa cópia do banco de prévia local, a migração foi aplicada e as 31 chaves foram validadas sem erro.

### Contagem de órfãos

```sql
SELECT 'FK_alerta_usuario_destinatario_id' AS restricao, count(*) AS orfaos FROM core.alerta x WHERE x.destinatario_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.usuario y WHERE y.id = x.destinatario_id)
UNION ALL
SELECT 'FK_cargo_empresa_empresa_id' AS restricao, count(*) AS orfaos FROM core.cargo x WHERE x.empresa_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.empresa y WHERE y.id = x.empresa_id)
UNION ALL
SELECT 'FK_documento_documento_tipo_tipo_id' AS restricao, count(*) AS orfaos FROM docs.documento x WHERE x.tipo_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM docs.documento_tipo y WHERE y.id = x.tipo_id)
UNION ALL
SELECT 'FK_documento_empresa_empresa_id' AS restricao, count(*) AS orfaos FROM docs.documento x WHERE x.empresa_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.empresa y WHERE y.id = x.empresa_id)
UNION ALL
SELECT 'FK_documento_setor_setor_id' AS restricao, count(*) AS orfaos FROM docs.documento x WHERE x.setor_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.setor y WHERE y.id = x.setor_id)
UNION ALL
SELECT 'FK_documento_unidade_unidade_id' AS restricao, count(*) AS orfaos FROM docs.documento x WHERE x.unidade_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.unidade y WHERE y.id = x.unidade_id)
UNION ALL
SELECT 'FK_documento_concessao_usuario_usuario_id' AS restricao, count(*) AS orfaos FROM docs.documento_concessao x WHERE x.usuario_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.usuario y WHERE y.id = x.usuario_id)
UNION ALL
SELECT 'FK_documento_versao_arquivo_objeto_arquivo_id' AS restricao, count(*) AS orfaos FROM docs.documento_versao x WHERE x.arquivo_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM docs.arquivo_objeto y WHERE y.id = x.arquivo_id)
UNION ALL
SELECT 'FK_documento_versao_documento_documento_id' AS restricao, count(*) AS orfaos FROM docs.documento_versao x WHERE x.documento_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM docs.documento y WHERE y.id = x.documento_id)
UNION ALL
SELECT 'FK_documento_vinculo_documento_documento_id' AS restricao, count(*) AS orfaos FROM docs.documento_vinculo x WHERE x.documento_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM docs.documento y WHERE y.id = x.documento_id)
UNION ALL
SELECT 'FK_equipe_empresa_empresa_id' AS restricao, count(*) AS orfaos FROM core.equipe x WHERE x.empresa_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.empresa y WHERE y.id = x.empresa_id)
UNION ALL
SELECT 'FK_equipe_trabalhador_chefe_id' AS restricao, count(*) AS orfaos FROM core.equipe x WHERE x.chefe_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.trabalhador y WHERE y.id = x.chefe_id)
UNION ALL
SELECT 'FK_equipe_unidade_local_id' AS restricao, count(*) AS orfaos FROM core.equipe x WHERE x.local_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.unidade y WHERE y.id = x.local_id)
UNION ALL
SELECT 'FK_perfil_permissao_perfil_perfil_id' AS restricao, count(*) AS orfaos FROM core.perfil_permissao x WHERE x.perfil_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.perfil y WHERE y.id = x.perfil_id)
UNION ALL
SELECT 'FK_perfil_permissao_permissao_permissao_id' AS restricao, count(*) AS orfaos FROM core.perfil_permissao x WHERE x.permissao_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.permissao y WHERE y.id = x.permissao_id)
UNION ALL
SELECT 'FK_setor_unidade_unidade_id' AS restricao, count(*) AS orfaos FROM core.setor x WHERE x.unidade_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.unidade y WHERE y.id = x.unidade_id)
UNION ALL
SELECT 'FK_trabalhador_cargo_cargo_id' AS restricao, count(*) AS orfaos FROM core.trabalhador x WHERE x.cargo_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.cargo y WHERE y.id = x.cargo_id)
UNION ALL
SELECT 'FK_trabalhador_empresa_empresa_id' AS restricao, count(*) AS orfaos FROM core.trabalhador x WHERE x.empresa_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.empresa y WHERE y.id = x.empresa_id)
UNION ALL
SELECT 'FK_trabalhador_setor_setor_id' AS restricao, count(*) AS orfaos FROM core.trabalhador x WHERE x.setor_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.setor y WHERE y.id = x.setor_id)
UNION ALL
SELECT 'FK_trabalhador_unidade_unidade_id' AS restricao, count(*) AS orfaos FROM core.trabalhador x WHERE x.unidade_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.unidade y WHERE y.id = x.unidade_id)
UNION ALL
SELECT 'FK_trabalhador_usuario_usuario_id' AS restricao, count(*) AS orfaos FROM core.trabalhador x WHERE x.usuario_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.usuario y WHERE y.id = x.usuario_id)
UNION ALL
SELECT 'FK_trabalhador_historico_cargo_cargo_id' AS restricao, count(*) AS orfaos FROM core.trabalhador_historico x WHERE x.cargo_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.cargo y WHERE y.id = x.cargo_id)
UNION ALL
SELECT 'FK_trabalhador_historico_setor_setor_id' AS restricao, count(*) AS orfaos FROM core.trabalhador_historico x WHERE x.setor_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.setor y WHERE y.id = x.setor_id)
UNION ALL
SELECT 'FK_trabalhador_historico_trabalhador_trabalhador_id' AS restricao, count(*) AS orfaos FROM core.trabalhador_historico x WHERE x.trabalhador_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.trabalhador y WHERE y.id = x.trabalhador_id)
UNION ALL
SELECT 'FK_unidade_empresa_empresa_id' AS restricao, count(*) AS orfaos FROM core.unidade x WHERE x.empresa_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.empresa y WHERE y.id = x.empresa_id)
UNION ALL
SELECT 'FK_usuario_escopo_usuario_usuario_id' AS restricao, count(*) AS orfaos FROM core.usuario_escopo x WHERE x.usuario_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.usuario y WHERE y.id = x.usuario_id)
UNION ALL
SELECT 'FK_usuario_perfil_empresa_empresa_id' AS restricao, count(*) AS orfaos FROM core.usuario_perfil x WHERE x.empresa_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.empresa y WHERE y.id = x.empresa_id)
UNION ALL
SELECT 'FK_usuario_perfil_perfil_perfil_id' AS restricao, count(*) AS orfaos FROM core.usuario_perfil x WHERE x.perfil_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.perfil y WHERE y.id = x.perfil_id)
UNION ALL
SELECT 'FK_usuario_perfil_usuario_usuario_id' AS restricao, count(*) AS orfaos FROM core.usuario_perfil x WHERE x.usuario_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.usuario y WHERE y.id = x.usuario_id)
UNION ALL
SELECT 'FK_usuario_sessao_usuario_usuario_id' AS restricao, count(*) AS orfaos FROM core.usuario_sessao x WHERE x.usuario_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.usuario y WHERE y.id = x.usuario_id)
UNION ALL
SELECT 'FK_usuario_token_redefinicao_senha_usuario_usuario_id' AS restricao, count(*) AS orfaos FROM core.usuario_token_redefinicao_senha x WHERE x.usuario_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM core.usuario y WHERE y.id = x.usuario_id);
```

### Validação

```sql
ALTER TABLE core.alerta VALIDATE CONSTRAINT "FK_alerta_usuario_destinatario_id";
ALTER TABLE core.cargo VALIDATE CONSTRAINT "FK_cargo_empresa_empresa_id";
ALTER TABLE docs.documento VALIDATE CONSTRAINT "FK_documento_documento_tipo_tipo_id";
ALTER TABLE docs.documento VALIDATE CONSTRAINT "FK_documento_empresa_empresa_id";
ALTER TABLE docs.documento VALIDATE CONSTRAINT "FK_documento_setor_setor_id";
ALTER TABLE docs.documento VALIDATE CONSTRAINT "FK_documento_unidade_unidade_id";
ALTER TABLE docs.documento_concessao VALIDATE CONSTRAINT "FK_documento_concessao_usuario_usuario_id";
ALTER TABLE docs.documento_versao VALIDATE CONSTRAINT "FK_documento_versao_arquivo_objeto_arquivo_id";
ALTER TABLE docs.documento_versao VALIDATE CONSTRAINT "FK_documento_versao_documento_documento_id";
ALTER TABLE docs.documento_vinculo VALIDATE CONSTRAINT "FK_documento_vinculo_documento_documento_id";
ALTER TABLE core.equipe VALIDATE CONSTRAINT "FK_equipe_empresa_empresa_id";
ALTER TABLE core.equipe VALIDATE CONSTRAINT "FK_equipe_trabalhador_chefe_id";
ALTER TABLE core.equipe VALIDATE CONSTRAINT "FK_equipe_unidade_local_id";
ALTER TABLE core.perfil_permissao VALIDATE CONSTRAINT "FK_perfil_permissao_perfil_perfil_id";
ALTER TABLE core.perfil_permissao VALIDATE CONSTRAINT "FK_perfil_permissao_permissao_permissao_id";
ALTER TABLE core.setor VALIDATE CONSTRAINT "FK_setor_unidade_unidade_id";
ALTER TABLE core.trabalhador VALIDATE CONSTRAINT "FK_trabalhador_cargo_cargo_id";
ALTER TABLE core.trabalhador VALIDATE CONSTRAINT "FK_trabalhador_empresa_empresa_id";
ALTER TABLE core.trabalhador VALIDATE CONSTRAINT "FK_trabalhador_setor_setor_id";
ALTER TABLE core.trabalhador VALIDATE CONSTRAINT "FK_trabalhador_unidade_unidade_id";
ALTER TABLE core.trabalhador VALIDATE CONSTRAINT "FK_trabalhador_usuario_usuario_id";
ALTER TABLE core.trabalhador_historico VALIDATE CONSTRAINT "FK_trabalhador_historico_cargo_cargo_id";
ALTER TABLE core.trabalhador_historico VALIDATE CONSTRAINT "FK_trabalhador_historico_setor_setor_id";
ALTER TABLE core.trabalhador_historico VALIDATE CONSTRAINT "FK_trabalhador_historico_trabalhador_trabalhador_id";
ALTER TABLE core.unidade VALIDATE CONSTRAINT "FK_unidade_empresa_empresa_id";
ALTER TABLE core.usuario_escopo VALIDATE CONSTRAINT "FK_usuario_escopo_usuario_usuario_id";
ALTER TABLE core.usuario_perfil VALIDATE CONSTRAINT "FK_usuario_perfil_empresa_empresa_id";
ALTER TABLE core.usuario_perfil VALIDATE CONSTRAINT "FK_usuario_perfil_perfil_perfil_id";
ALTER TABLE core.usuario_perfil VALIDATE CONSTRAINT "FK_usuario_perfil_usuario_usuario_id";
ALTER TABLE core.usuario_sessao VALIDATE CONSTRAINT "FK_usuario_sessao_usuario_usuario_id";
ALTER TABLE core.usuario_token_redefinicao_senha VALIDATE CONSTRAINT "FK_usuario_token_redefinicao_senha_usuario_usuario_id";
```
