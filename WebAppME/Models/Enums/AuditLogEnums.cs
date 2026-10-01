namespace BatteryTestingSystem.Models.Enums;

public enum ModuleName
{
    NONE = 0,
    USER= 1,
    SYSTEM =2,
    CIRCUIT=4,
    PROGRAM =5,
    BATTERY =6,
    REGISTRATON = 7,
    CALIBRATION = 8,
    TESTING = 9,
    REPORTING = 10,
    DBC = 11,
    SERVICE = 12,
    REGISTER = 13,
}

public enum AuditActionType
{
    NONE = 0,
    CREATE = 1,
    UPDATE = 2,
    DELETE = 3,
    LOGIN = 4,
    LOGOUT = 5,
    VIEW = 6,
    UPLOAD = 7,
    TRANSFER = 8,
    ERROR = 9,
    OTHER = 10,
    START = 11,
    STOP = 12,
    PAUSE = 13,
    CONTINUE = 14,
    EXPORT = 14,
    IMPORT = 16,
    VISIT = 17,
    DOWNLOAD = 18,
    CONFIGURE = 19,
    APPROVE = 20,
    REJECT = 21,
}

public enum SeverityLevel
{
    INFO = 1,
    WARNING = 2,
    ERROR = 3,
    CRITICAL = 4
}