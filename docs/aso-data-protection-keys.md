# Chaves do Data Protection (registro de ASO)

O registro administrativo de ASO guarda aptidão, função avaliada e identificação do emitente (nome, CRM e UF) na coluna `saude.aso_registro.dados_protegidos`. O conteúdo é cifrado pelo ASP.NET Core Data Protection (propósito `SESMT.OccupationalHealth.v1`, aplicação `SESMT.API`).

**O backup do PostgreSQL sozinho não recupera esses dados.** Sem o diretório de chaves usado na gravação, a API não consegue decifrar a coluna. Nesse caso ela responde 503 `ASO_UNAVAILABLE` e não inventa aptidão.

## Configuração

| Ambiente | Comportamento |
| --- | --- |
| `Development` sem `DataProtection__KeysPath` | Usa o local padrão do ASP.NET Core (no Windows, o perfil do usuário). |
| `Testing` | Chaves temporárias; usado apenas pelos testes de integração. |
| Qualquer outro ambiente | `DataProtection__KeysPath` é obrigatório; sem ele a API não inicia. |

No `docker-compose.yml`, a API usa `DataProtection__KeysPath=/keys`, montado no volume nomeado `dataprotection_keys`. A imagem cria `/keys` com dono igual ao usuário não-root da aplicação e permissão `700`.

Os arquivos de chave são XML. No Linux, eles ficam sem proteção adicional em repouso. Por isso:

- restrinja o acesso ao volume ou diretório como se fosse uma credencial;
- não versione o diretório nem o copie para o mesmo local dos backups do banco;
- em produção, avalie proteger as chaves com certificado (`ProtectKeysWithCertificate`) ou com um cofre gerenciado.

## Backup

Faça o backup das chaves junto com cada backup do banco, mas guarde os dois em locais separados:

```bash
docker run --rm -v sesmt-backend_dataprotection_keys:/keys -v "$PWD":/backup alpine \
  tar czf /backup/sesmt-dataprotection-keys-$(date +%F).tgz -C /keys .
```

O nome do volume segue o padrão `<projeto>_dataprotection_keys`; confira com `docker volume ls`.

## Restauração

1. Restaure o banco a partir do backup.
2. Restaure as chaves no volume ou diretório configurado **antes** de iniciar a API:

   ```bash
   docker run --rm -v sesmt-backend_dataprotection_keys:/keys -v "$PWD":/backup alpine \
     sh -c "tar xzf /backup/sesmt-dataprotection-keys-AAAA-MM-DD.tgz -C /keys && chown -R 1654 /keys && chmod 700 /keys"
   ```

3. Inicie a API e abra um registro de ASO existente para confirmar que os dados aparecem.

Não apague chaves antigas: elas continuam necessárias para decifrar os registros gravados antes da rotação automática.
