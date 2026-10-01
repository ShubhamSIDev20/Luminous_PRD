---
name: gortex-me-primary-src-me-log
description: "Work in the me-primary/src · me_log area — 79 symbols across 16 files (98% cohesion)"
---

# me-primary/src · me_log

79 symbols | 16 files | 98% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\src\app_queues.c`
- `ME Project\me-primary\src\exec\step_engine.c`
- `ME Project\me-primary\src\main.c`
- `ME Project\me-primary\src\net\tcp_client.c`
- `ME Project\me-primary\src\net\udp_sock.c`
- `ME Project\me-primary\src\platform\netinfo.c`
- `ME Project\me-primary\src\platform\rpmsg_link.c`
- `ME Project\me-primary\src\store\circuit_store.c`
- `ME Project\me-primary\src\sys_init.c`
- `ME Project\me-primary\src\threads\can_mgr.c`
- `ME Project\me-primary\src\threads\comm_thread.c`
- `ME Project\me-primary\src\threads\core_logic.c`
- `ME Project\me-primary\src\threads\data_mgr.c`
- `ME Project\me-primary\src\threads\post_reg.c`
- `ME Project\me-primary\src\util\log.h`
- `ME Project\me-primary\src\util\msgq.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\src\app_queues.c` | me_app_request_stop, me_queues_init, me_queues_destroy |
| `ME Project\me-primary\src\exec\step_engine.c` | me_exec_on_response_timeout |
| `ME Project\me-primary\src\main.c` | usage, parse_args, main, install_signal_handlers, on_signal |
| `ME Project\me-primary\src\net\tcp_client.c` | me_tcp_connect, me_tcp_send_all, me_tcp_recv_response, set_nonblocking |
| `ME Project\me-primary\src\net\udp_sock.c` | me_udp_send_to, me_udp_open, me_udp_dest_init |
| `ME Project\me-primary\src\platform\netinfo.c` | find_fallback_iface, read_named_iface, me_netinfo_read |
| `ME Project\me-primary\src\platform\rpmsg_link.c` | me_rpmsg_open, me_rpmsg_write_all |
| `ME Project\me-primary\src\store\circuit_store.c` | me_store_program_append |
| `ME Project\me-primary\src\sys_init.c` | me_config_defaults, me_sys_init, me_sys_shutdown |
| `ME Project\me-primary\src\threads\can_mgr.c` | handle_can_tx, drain_device, log_tx_frame, forward_to_core, drain_queue, ... |
| `ME Project\me-primary\src\threads\comm_thread.c` | me_comm_thread_start, comm_thread_main, handle_program_handshake, print_registered_banner, consume_rx_buffer, ... |
| `ME Project\me-primary\src\threads\core_logic.c` | handle, core_logic_main, on_program_loaded, me_execute_program, now_ms, ... |
| `ME Project\me-primary\src\threads\data_mgr.c` | ring_push, reply_not_found, serve_battery, me_data_mgr_start, serve_program, ... |
| `ME Project\me-primary\src\threads\post_reg.c` | me_post_reg_send |
| `ME Project\me-primary\src\util\log.h` | ME_LOGD, ME_LOGE, ME_LOGI, me_log, ME_LOGW |
| `ME Project\me-primary\src\util\msgq.c` | me_msgq_init |

## Entry Points

- `ME Project\me-primary\src\main.c::main`
- `ME Project\me-primary\src\threads\comm_thread.c::comm_thread_main`
- `ME Project\me-primary\src\net\tcp_client.c::me_tcp_connect`
- `ME Project\me-primary\src\threads\can_mgr.c::can_mgr_main`
- `ME Project\me-primary\src\util\msgq.c::me_msgq_init`

## Connected Communities

- **src/store** (1 cross-edges)
- **src/exec** (1 cross-edges)
- **me-primary/src · me_queues_report** (1 cross-edges)

## How to Explore

```
analyze(operation:"communities", id:"community-17")
explore(operation:"context", task:"understand me-primary/src · me_log", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\src\main.c::main"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
