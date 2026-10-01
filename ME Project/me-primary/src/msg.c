/*
 * msg.c - printable names for message types and statuses.
 *
 * Every inter-thread hand-off is logged by name. A numeric type in a log line
 * is useless at 2am; a name is not.
 */
#include "msg.h"

const char *me_msg_type_name(me_msg_type_t t)
{
    switch (t) {
    case ME_MSG_NONE:              return "NONE";
    case ME_MSG_STORE_PROGRAM:     return "STORE_PROGRAM";
    case ME_MSG_STORE_BATTERY:     return "STORE_BATTERY";
    case ME_MSG_STORE_CONFIG:      return "STORE_CONFIG";
    case ME_MSG_CONTROL:           return "CONTROL";
    case ME_MSG_REQ_PROGRAM:       return "REQ_PROGRAM";
    case ME_MSG_REQ_BATTERY:       return "REQ_BATTERY";
    case ME_MSG_RSP_PROGRAM_CHUNK: return "RSP_PROGRAM_CHUNK";
    case ME_MSG_RSP_BATTERY:       return "RSP_BATTERY";
    case ME_MSG_RSP_NOT_FOUND:     return "RSP_NOT_FOUND";
    case ME_MSG_REALTIME_DATA:     return "REALTIME_DATA";
    case ME_MSG_SESSION_DATA:      return "SESSION_DATA";
    case ME_MSG_CAN_TX:            return "CAN_TX";
    case ME_MSG_CAN_DATA:          return "CAN_DATA";
    case ME_MSG_TYPE_COUNT:        break;
    default:                       break;
    }
    return "UNKNOWN";
}

const char *me_msg_status_name(me_msg_status_t s)
{
    switch (s) {
    case ME_STATUS_OK:                  return "OK";
    case ME_STATUS_NO_PROGRAM:          return "NO_PROGRAM";
    case ME_STATUS_PROGRAM_INCOMPLETE:  return "PROGRAM_INCOMPLETE";
    case ME_STATUS_PROGRAM_INVALID:     return "PROGRAM_INVALID";
    case ME_STATUS_NO_BATTERY:          return "NO_BATTERY";
    case ME_STATUS_BAD_CIRCUIT:         return "BAD_CIRCUIT";
    case ME_STATUS_COUNT:               break;
    default:                            break;
    }
    return "UNKNOWN";
}
