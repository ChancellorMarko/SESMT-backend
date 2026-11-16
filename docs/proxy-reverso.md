# API atrás de proxy reverso

Atrás de um proxy (nginx, Traefik, balanceador), a conexão que chega à API vem do proxy. Sem configuração:
- a auditoria grava o IP do proxy, não o do usuário;
- o limite de tentativas por cliente passa a valer para todos os usuários juntos.

Liste os proxies confiáveis:

```json
"ForwardedHeaders": { "KnownProxies": ["10.0.0.5"] }
```

ou, por variável de ambiente, `ForwardedHeaders__KnownProxies__0=10.0.0.5`.

- **Com a lista preenchida:** a API usa `X-Forwarded-For` e `X-Forwarded-Proto`, mas só quando a conexão vem de um desses endereços. Um cliente comum não consegue forjar o próprio IP.
- **Com a lista vazia (padrão):** os cabeçalhos são ignorados.
- **Endereço inválido na lista:** a API não inicia.
