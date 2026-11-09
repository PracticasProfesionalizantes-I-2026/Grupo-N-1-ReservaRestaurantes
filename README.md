# Grupo-N-1-ReservaRestaurantes

Martín Cagliero y Jerónimo Benassi

## Documentación

https://drive.google.com/drive/folders/1cNzwDWj6T90z0Tu3F36VH9q2dUODGaHm?usp=drive\_link

Link Mockups: https://app.milanote.com/1Wo0vq1YlSbddo?p=a2k8d9DKRk0

Link Casos de uso: https://docs.google.com/document/d/179PLOcjdmDZN2ttpPOnyVCjfAl-JFYf-usyJHQH8-G8/edit?usp=sharing

## Credenciales Demo / Semilla

Al inicializar la base de datos con `DbInitializer`, se crean los siguientes usuarios semilla para desarrollo y pruebas:

| Rol | Email | Contraseña Demo | Variable de Entorno Opcional |
|---|---|---|---|
| Cliente | `juan.perez@example.com` | `Cliente123!` | `CLIENTE_PASSWORD` |
| Cliente | `maria.gonzalez@example.com` | `Cliente123!` | `CLIENTE_PASSWORD` |
| Cliente | `carlos.rodriguez@example.com` | `Cliente123!` | `CLIENTE_PASSWORD` |
| Cliente | `laura.fernandez@example.com` | `Cliente123!` | `CLIENTE_PASSWORD` |
| Gerente | `gerente@restaurant.com` | `Gerente123!` | `GERENTE_PASSWORD` |
| Administrador | `admin@restaurant.com` | `Admin123!` | `ADMIN_PASSWORD` |

Todos los hashes se almacenan utilizando BCrypt y son aceptados directamente por el endpoint de autenticación (`/api/auth/login`).

