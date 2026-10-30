-- Aerion Airline Management - SQL Server schema (CSAI 202)
IF DB_ID('AerionAirline') IS NULL CREATE DATABASE AerionAirline;
GO
USE AerionAirline;
GO

CREATE TABLE UserAccount (
    UserID       INT IDENTITY PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash NVARCHAR(200) NOT NULL,
    Role         NVARCHAR(20)  NOT NULL CHECK (Role IN ('passenger','staff','admin'))
);

CREATE TABLE Passenger (
    PassengerID    INT IDENTITY PRIMARY KEY,
    UserID         INT NOT NULL UNIQUE REFERENCES UserAccount(UserID),
    Name           NVARCHAR(100) NOT NULL,
    PassportNumber NVARCHAR(20)  NOT NULL UNIQUE,
    Contact        NVARCHAR(200) NULL
);

CREATE TABLE Employee (
    EmployeeID INT IDENTITY PRIMARY KEY,
    UserID     INT NOT NULL UNIQUE REFERENCES UserAccount(UserID),
    Name       NVARCHAR(100) NOT NULL,
    Position   NVARCHAR(50)  NOT NULL
);

CREATE TABLE CrewMember (
    CrewID    INT IDENTITY PRIMARY KEY,
    Name      NVARCHAR(100) NOT NULL,
    CrewRole  NVARCHAR(30)  NOT NULL CHECK (CrewRole IN ('pilot','co-pilot','cabin crew')),
    LicenseNo NVARCHAR(30)  NULL
);

CREATE TABLE Airport (
    AirportID INT IDENTITY PRIMARY KEY,
    Code      NVARCHAR(5)   NOT NULL UNIQUE,
    Name      NVARCHAR(100) NOT NULL,
    City      NVARCHAR(60)  NOT NULL,
    Country   NVARCHAR(60)  NOT NULL
);

CREATE TABLE Route (
    RouteID            INT IDENTITY PRIMARY KEY,
    DepartureAirportID INT NOT NULL REFERENCES Airport(AirportID),
    ArrivalAirportID   INT NOT NULL REFERENCES Airport(AirportID),
    DistanceKm         INT NOT NULL CHECK (DistanceKm > 0),
    BaseFare           DECIMAL(10,2) NOT NULL CHECK (BaseFare > 0),
    CONSTRAINT UQ_Route UNIQUE (DepartureAirportID, ArrivalAirportID),
    CONSTRAINT CK_Route_Diff CHECK (DepartureAirportID <> ArrivalAirportID)
);

CREATE TABLE Aircraft (
    AircraftID         INT IDENTITY PRIMARY KEY,
    RegistrationNumber NVARCHAR(20) NOT NULL UNIQUE,
    Model              NVARCHAR(50) NOT NULL,
    TotalSeats         INT NOT NULL CHECK (TotalSeats BETWEEN 20 AND 500)
);

CREATE TABLE FareClass (
    FareClassID     INT IDENTITY PRIMARY KEY,
    Name            NVARCHAR(20) NOT NULL UNIQUE,
    PriceMultiplier DECIMAL(4,2) NOT NULL CHECK (PriceMultiplier > 0),
    MilesMultiplier DECIMAL(4,2) NOT NULL CHECK (MilesMultiplier > 0)
);

CREATE TABLE Flight (
    FlightID      INT IDENTITY PRIMARY KEY,
    RouteID       INT NOT NULL REFERENCES Route(RouteID),
    AircraftID    INT NOT NULL REFERENCES Aircraft(AircraftID),
    FlightNumber  NVARCHAR(10) NOT NULL,
    DepartureTime DATETIME2 NOT NULL,
    ArrivalTime   DATETIME2 NOT NULL,
    Status        NVARCHAR(20) NOT NULL DEFAULT 'on time' CHECK (Status IN ('on time','delayed','cancelled')),
    CONSTRAINT CK_Flight_Times CHECK (ArrivalTime > DepartureTime)
);

CREATE TABLE FlightCrew (
    FlightID INT NOT NULL REFERENCES Flight(FlightID),
    CrewID   INT NOT NULL REFERENCES CrewMember(CrewID),
    PRIMARY KEY (FlightID, CrewID)
);

CREATE TABLE Seat (
    SeatID      INT IDENTITY PRIMARY KEY,
    FlightID    INT NOT NULL REFERENCES Flight(FlightID),
    SeatNumber  NVARCHAR(5) NOT NULL,
    FareClassID INT NOT NULL REFERENCES FareClass(FareClassID),
    IsAvailable BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Seat UNIQUE (FlightID, SeatNumber)
);

CREATE TABLE Booking (
    BookingID          INT IDENTITY PRIMARY KEY,
    PassengerID        INT NOT NULL REFERENCES Passenger(PassengerID),
    FlightID           INT NOT NULL REFERENCES Flight(FlightID),
    BookedByEmployeeID INT NULL REFERENCES Employee(EmployeeID),
    BookingDate        DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    Status             NVARCHAR(20) NOT NULL DEFAULT 'confirmed' CHECK (Status IN ('confirmed','cancelled')),
    DiscountPercent    INT NOT NULL DEFAULT 0,
    MilesRedeemed      INT NOT NULL DEFAULT 0,
    MilesEarned        INT NOT NULL DEFAULT 0,
    CancelledAt        DATETIME2 NULL
);

CREATE TABLE Ticket (
    TicketID   INT IDENTITY PRIMARY KEY,
    BookingID  INT NOT NULL REFERENCES Booking(BookingID),
    FlightID   INT NOT NULL REFERENCES Flight(FlightID),
    SeatNumber NVARCHAR(5) NULL,
    FarePaid   DECIMAL(10,2) NOT NULL,
    Status     NVARCHAR(20) NOT NULL DEFAULT 'issued' CHECK (Status IN ('issued','checked in','cancelled'))
);

CREATE TABLE Payment (
    PaymentID INT IDENTITY PRIMARY KEY,
    BookingID INT NOT NULL REFERENCES Booking(BookingID),
    Amount    DECIMAL(10,2) NOT NULL,
    Method    NVARCHAR(20) NOT NULL CHECK (Method IN ('card','cash','bank transfer')),
    Status    NVARCHAR(20) NOT NULL DEFAULT 'paid' CHECK (Status IN ('paid','refunded')),
    PaidAt    DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);

CREATE TABLE Baggage (
    BaggageID INT IDENTITY PRIMARY KEY,
    TicketID  INT NOT NULL REFERENCES Ticket(TicketID),
    TagNumber NVARCHAR(20) NOT NULL UNIQUE,
    WeightKg  DECIMAL(5,1) NOT NULL DEFAULT 0,
    Status    NVARCHAR(20) NOT NULL DEFAULT 'not checked in'
              CHECK (Status IN ('not checked in','checked in','loaded','delayed','lost','delivered'))
);

CREATE TABLE LoyaltyAccount (
    LoyaltyID    INT IDENTITY PRIMARY KEY,
    PassengerID  INT NOT NULL UNIQUE REFERENCES Passenger(PassengerID),
    MilesBalance INT NOT NULL DEFAULT 0 CHECK (MilesBalance >= 0)
);

CREATE TABLE LoyaltyTransaction (
    TransactionID INT IDENTITY PRIMARY KEY,
    LoyaltyID     INT NOT NULL REFERENCES LoyaltyAccount(LoyaltyID),
    Miles         INT NOT NULL,
    Reason        NVARCHAR(100) NOT NULL,
    CreatedAt     DATETIME2 NOT NULL DEFAULT SYSDATETIME()
);
GO
CREATE UNIQUE INDEX UX_Ticket_Seat ON Ticket(FlightID, SeatNumber) WHERE SeatNumber IS NOT NULL;
GO
