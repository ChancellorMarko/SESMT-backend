# E-mail de redefinição de senha em produção

## Configuração da aplicação

A aplicação lê a seção `PasswordResetEmail` pela configuração padrão do .NET. No ambiente de produção, injete estes valores pelo mecanismo de segredos da plataforma (secret store para credenciais e configuração protegida para os demais valores). Não grave valores reais em `appsettings*.json`, `docker-compose.yml`, imagens ou no repositório.

| Variável | Finalidade |
| --- | --- |
| `PasswordResetEmail__Host` | Host SMTP do provedor escolhido |
| `PasswordResetEmail__Port` | Porta SMTP fornecida pelo provedor |
| `PasswordResetEmail__EnableSsl` | Deve ser `true` em produção (STARTTLS/TLS) |
| `PasswordResetEmail__From` | Endereço remetente verificado pelo provedor |
| `PasswordResetEmail__Username` | Usuário SMTP, injetado pelo secret store |
| `PasswordResetEmail__Password` | Senha ou app password SMTP, injetada pelo secret store |
| `PasswordResetEmail__ResetUrl` | URL HTTPS da página de redefinição no front-end |

O ambiente `Production` valida a configuração durante a inicialização e não sobe se host, porta, TLS, remetente, credenciais ou URL HTTPS estiverem ausentes/inválidos. O envio não registra destinatário, token, credenciais ou payload da exceção. Falhas são registradas com o tipo da exceção e a API mantém resposta genérica no pedido de recuperação para não revelar se a conta existe.

O link inclui o token como parâmetro `token` na URL configurada. A página do front-end deve ler esse parâmetro e enviá-lo ao `POST /auth/reset-password` junto com a nova senha. Configure a camada web para não registrar query strings dessa rota e defina uma política `Referrer-Policy: no-referrer`.

## Publicação e rotação

1. Escolha um provedor com envio transacional, autentique o domínio e verifique o remetente.
2. Cadastre host e porta exatamente como publicados pelo provedor; habilite TLS.
3. Crie um segredo exclusivo para a aplicação e injete usuário e senha no processo/container. Restrinja leitura ao serviço SESMT e habilite auditoria de acesso ao segredo.
4. Configure os registros SPF e DKIM indicados pelo provedor no DNS do domínio. Publique DMARC inicialmente em modo de monitoramento (`p=none`), avalie relatórios e só então aperte a política. Não crie um segundo SPF; consolide os emissores autorizados no registro existente.
5. Configure `ResetUrl` para a página HTTPS do front-end e publique a aplicação com as variáveis protegidas.
6. Monitore o evento `Falha ao enviar e-mail de recuperação de senha` e alertas de inicialização por configuração inválida. Nunca habilite logging de corpo da requisição ou do e-mail.
7. Para rotação, crie nova credencial no provedor, atualize o secret store, reinicie/recicle as instâncias, valide uma recuperação completa e revogue a credencial antiga. Não faça rotação por edição de arquivo no repositório.

## Validação operacional antes de concluir o card

- Confirmar SPF, DKIM e DMARC em DNS e verificar a autenticação no cabeçalho da mensagem entregue.
- Solicitar recuperação para uma conta de teste e confirmar entrega no destino e ausência de token nos logs.
- Abrir o link, enviar o token ao endpoint de redefinição, confirmar sucesso e confirmar que o mesmo token não pode ser usado de novo.
- Confirmar que a senha anterior não autentica, a nova senha autentica e as sessões de refresh existentes foram revogadas.
- Forçar uma falha de SMTP em ambiente controlado e confirmar alerta nos logs sem mudança na resposta genérica de recuperação.

Este repositório não determina o provedor SMTP, domínio, destino de produção, gerenciador de segredos ou registros DNS. Esses valores precisam ser fornecidos pelo responsável pelo ambiente antes da publicação e validação real.

