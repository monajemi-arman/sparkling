namespace Sparkling.Backend.Exceptions;

internal class NonRetryableException(string msg) : Exception(msg);