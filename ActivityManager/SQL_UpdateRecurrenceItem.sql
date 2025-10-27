CREATE PROCEDURE [dbo].[ice_updateRecurrenceItem]
	@ActivityItemId uniqueidentifier
AS

DECLARE @JSONSCHEDULE nvarchar(MAX),
@STARTDATE datetime,
@ENDDATE datetime,
@INTERVAL int,
@STARTTIME varchar(6),
@DURATION varchar(6),
@FIELDVALUE nvarchar(100),
@OFFSETHOUR int,
@OFFSETMINUTE int,
@OFFSET nvarchar(10),
@OFFSETABS varchar(1),
@STARTTIMEHOUR int,
@STARTTIMEMINUTE int,
@DURATIONHOUR int,
@DURATIONMINUTE int,
@FINDINDEX int,
@FREQUENCYID int


---- JSON example for week
--SET @JSONSCHEDULE = N'{
--    "timezoneOffset": "+8:30",
--	 "startDate": "2017-08-01",			
--    "recurrence":								
--    {
--        "frequency": "week",					
--        "interval": 1,						
--        "schedule":	
--        {
--            "weekDays": ["mo", "we", "fr"],	
--            "startTime": "10:30",
--			"duration": "5:30"
--        },
--        "count": 10,							
--        "endDate": "2017-08-10"		
--    }
--}'

--- #1 Get the values needed to process the recurrence
SET @JSONSCHEDULE = (SELECT TOP 1 fv.[Value] FROM FieldValue fv
join ActivityItemFieldValues aifv on aifv.FieldValueId = fv.Id
join Field f on f.Id = fv.FieldId
WHERE f.Name = 'Recurrence' and 
aifv.ActivityItemContainerId = @ActivityItemId)

SET @OFFSET = (SELECT JSON_VALUE(@JSONSCHEDULE, N'$.timezoneOffset'))
SET @FINDINDEX = CHARINDEX(':',@OFFSET,  1)

SET @OFFSETABS = SUBSTRING(@OFFSET, 1, 1) 
IF (@OFFSETABS != '-' AND @OFFSETABS != '+') 
BEGIN
	SET @OFFSETABS = '+'
END

SET @OFFSETHOUR = SUBSTRING(@OFFSET, 0,@FINDINDEX)
SET @OFFSETHOUR = ABS(@OFFSETHOUR)
SET @OFFSETMINUTE = SUBSTRING(@OFFSET, @FINDINDEX+1, LEN(@OFFSET)-@FINDINDEX)

IF (@OFFSETABS = '-')
BEGIN
	SET @OFFSETHOUR = @OFFSETHOUR * -1
	SET @OFFSETMINUTE = @OFFSETMINUTE * -1
END

SET @FREQUENCYID = (SELECT Id FROM RecurrenceFrequency WHERE LOWER([Name]) = LOWER(JSON_VALUE(@JSONSCHEDULE, N'$.recurrence.frequency')))

SET @STARTTIME = (SELECT JSON_VALUE(@JSONSCHEDULE, N'$.recurrence.schedule.startTime'))
SET @FINDINDEX = CHARINDEX(':',@STARTTIME,  1)
SET @STARTTIMEHOUR = SUBSTRING(@STARTTIME, 0, @FINDINDEX)
SET @STARTTIMEMINUTE = SUBSTRING(@STARTTIME, @FINDINDEX+1, LEN(@STARTTIME)-@FINDINDEX)

SET @DURATION = (SELECT JSON_VALUE(@JSONSCHEDULE, N'$.recurrence.schedule.duration'))
SET @FINDINDEX = CHARINDEX(':',@DURATION,  1)
SET @DURATIONHOUR = SUBSTRING(@DURATION, 0, @FINDINDEX)
SET @DURATIONMINUTE = SUBSTRING(@DURATION, @FINDINDEX+1, LEN(@DURATION)-@FINDINDEX)

SET @FIELDVALUE = (SELECT JSON_VALUE(@JSONSCHEDULE, N'$.recurrence.interval'))
SET @INTERVAL = CONVERT(int, @FIELDVALUE)

SET @FIELDVALUE = (SELECT JSON_VALUE(@JSONSCHEDULE, N'$.startDate'))
SET @STARTDATE = CONVERT(datetime, @FIELDVALUE)

SET @FIELDVALUE = (SELECT JSON_VALUE(@JSONSCHEDULE, N'$.recurrence.endDate'))
SET @ENDDATE = CONVERT(datetime, @FIELDVALUE)

DECLARE @WEEKDAYS as nvarchar(40)
SET @WEEKDAYS = (SELECT JSON_QUERY(@JSONSCHEDULE, N'$.recurrence.schedule.weekDays'))

DECLARE @DAYSOFWEEK TABLE (dow varchar(2), dowInt int, isSelected bit)
INSERT INTO @DAYSOFWEEK Values ('su',1, isnull((SELECT 1 FROM OPENJSON(@WEEKDAYS) where value = 'su'), 0)), 
('mo',2,isnull((SELECT 1 FROM OPENJSON(@WEEKDAYS) where value = 'mo'), 0)), 
('tu',3, isnull((SELECT 1 FROM OPENJSON(@WEEKDAYS) where value = 'tu'), 0)), 
('we',4, isnull((SELECT 1 FROM OPENJSON(@WEEKDAYS) where value = 'we'), 0)), 
('th',5, isnull((SELECT 1 FROM OPENJSON(@WEEKDAYS) where value = 'th'), 0)), 
('fr',6, isnull((SELECT 1 FROM OPENJSON(@WEEKDAYS) where value = 'fr'), 0)), 
('sa',7, isnull((SELECT 1 FROM OPENJSON(@WEEKDAYS) where value = 'sa'), 0))

--- #2 Create the item in ActivityItemRecurrence table or retrieve the existing one
DECLARE @AIRID int
SET @AIRID = (SELECT TOP 1 air.Id FROM ActivityItemRecurrence air WHERE air.ActivityItemContainerId = @ActivityItemId)
IF @AIRID is null
BEGIN
	INSERT INTO ActivityItemRecurrence (ActivityItemContainerId, StartDateTime, EndDateTime, FrequencyId)
	VALUES (@ActivityItemId, @STARTDATE, @ENDDATE, @FREQUENCYID)
	SET @AIRID = @@IDENTITY
END
ELSE
BEGIN
	UPDATE ActivityItemRecurrence 
	SET StartDateTime = @STARTDATE,
	EndDateTime = @ENDDATE,
	FrequencyId = @FREQUENCYID
	WHERE Id = @AIRID
END

--- #3 Create the RecurrenceSchedule
DECLARE @RESCH TABLE (id int, EndDateTime datetime, Schedule nvarchar(max))
INSERT INTO @RESCH SELECT rs.Id, rs.EndDateTime, rs.Schedule FROM RecurrenceSchedule rs WHERE rs.ActivityItemRecurrenceId = @AIRID

-- Get the last updated schedule
DECLARE @LATESTRESCHID int = (SELECT TOP 1 Id FROM @RESCH ORDER BY EndDateTime desc)

IF exists(SELECT * FROM @RESCH)
BEGIN
	--- Now do some crazy work to readjust the recurrence based on if there is an update.
	DECLARE @CURSCHEDULE nvarchar(max) = (SELECT Schedule FROM RecurrenceSchedule WHERE Id = @LATESTRESCHID)

	if (@CURSCHEDULE != @JSONSCHEDULE)
	BEGIN
		-- First delete the items that are in the future or have not been Unvirtualized.
		DECLARE @DELID TABLE (id uniqueidentifier, isSoftDelete bit)
		INSERT INTO @DELID SELECT vi.Id, CASE WHEN (vr.StartDateTime < GETUTCDATE() AND vi.ActivityItemContainerUnvirtualId is null) THEN 1 WHEN (vr.StartDateTime > GETUTCDATE()) THEN 0 ELSE 2 END FROM VirtualItem vi 
							JOIN VirtualRecurrence vr on vr.VirtualItemId = vi.Id 
							JOIN RecurrenceSchedule rs on rs.Id = vr.RecurrenceScheduleId
							JOIN ActivityItemRecurrence air on air.Id = rs.ActivityItemRecurrenceId
							WHERE air.ActivityItemContainerId = @ActivityItemId

		-- These are hard deletes per they are in the future and are no longer needed.
		DELETE FROM VirtualRecurrence WHERE VirtualItemId in (SELECT Id FROM @DELID WHERE isSoftDelete = 0)
		DELETE FROM VirtualItem WHERE Id in (SELECT Id FROM @DELID WHERE isSoftDelete = 0)
		DELETE FROM ActivityItemIndex WHERE ActivityItemContainerId in (SELECT Id FROM @DELID WHERE isSoftDelete = 0)

		-- These are soft deletes per they are in the past so do not want to necessarily delete them even if they are only virtualized.  The RecurrenceSchedule will still be there but there will be a new one.
		UPDATE VirtualItem SET StateId = 5 WHERE Id in (SELECT Id FROM @DELID WHERE isSoftDelete = 1)
		UPDATE ActivityItemIndex SET StateId = 5,ModifiedDate = GETUTCDATE() WHERE ActivityItemContainerId in (SELECT Id FROM @DELID WHERE isSoftDelete = 1)

		UPDATE RecurrenceSchedule SET EndDateTime = GETUTCDATE()

		INSERT INTO RecurrenceSchedule (ActivityItemRecurrenceId, Schedule, ProcessDateTime, EndDateTime, Interval)
		VALUES (@AIRID, @JSONSCHEDULE, GETUTCDATE(), @ENDDATE, @INTERVAL)

		SET @LATESTRESCHID = @@IDENTITY
		-- Need to set the Start Date to Now
		SET @STARTDATE = CONVERT(date, GETUTCDATE())
	END
END
ELSE
BEGIN
	INSERT INTO RecurrenceSchedule (ActivityItemRecurrenceId, Schedule, ProcessDateTime, EndDateTime, Interval)
	VALUES (@AIRID, @JSONSCHEDULE, GETUTCDATE(), @ENDDATE, @INTERVAL)

	SET @LATESTRESCHID = @@IDENTITY
END

--- #4 Now create the VirtualItem(s) based on WEEK Frequency
DECLARE @FIRSTRECUREVENT datetime
DECLARE @LASTRECUREVENT datetime
DECLARE @EVENTDATE datetime = @STARTDATE
DECLARE @STARTDAYOFWEEK int = (select DATEPART(dw,@STARTDATE))
DECLARE @CURRENTINTERVAL int = 0
DECLARE @RECURSTART datetime, @RECUREND datetime
DECLARE @VIRTUALTABLE table (Id uniqueidentifier)
WHILE (@EVENTDATE <= @ENDDATE)
BEGIN
	DECLARE @EVENTDAY int = DATEPART(dw, @EVENTDATE)
	IF (@EVENTDAY = 1 AND @EVENTDATE != @STARTDATE)
	BEGIN
		SET @CURRENTINTERVAL = @CURRENTINTERVAL+1
	END

	IF (@CURRENTINTERVAL % @INTERVAL = 0)
	BEGIN
		if exists(SELECT * FROM @DAYSOFWEEK WHERE @EVENTDAY = dowInt and isSelected = 1)
		BEGIN
			-- Create the Start Date/Time based on the StartTime
			SET @RECURSTART = @EVENTDATE + @STARTTIME
			-- SET @RECURSTART = DATEADD(HOUR,(-1 * @OFFSETHOUR), @RECURSTART)
			-- SET @RECURSTART = DATEADD(MINUTE,(-1* @OFFSETMINUTE), @RECURSTART)
			SET @RECUREND = @RECURSTART + @DURATION
			
			if not exists(SELECT * FROM VirtualRecurrence WHERE RecurrenceScheduleId = @LATESTRESCHID AND StartDateTime = @RECURSTART AND DueDateTime = @RECUREND)
			BEGIN
				DECLARE @ACTIVESTATE int = (SELECT TOP 1 Id FROM EntityStatus WHERE  ObjectType = '110' and [Name] = 'Active')
				DECLARE @VirtualId uniqueidentifier
				-- Create the virtual item
				INSERT INTO VirtualItem (StateId) output inserted.Id INTO @VIRTUALTABLE VALUES (@ACTIVESTATE)
				SET @VirtualId = (SELECT TOP 1 Id FROM @VIRTUALTABLE)
				
				--  Just temporary to avoid the following 'Warning: Null value is eliminated by an aggregate or other SET operation.'
				SET ANSI_WARNINGS OFF
				INSERT INTO ActivityItemIndex (ActivityItemContainerId, CustomerId, ActivityTemplateId, Title, AssignedTo, CreatedById, DueDateTime, StartDateTime, EndDateTime, ItemStatus, AssignedLocationId, StateId, IsParent)
				SELECT
				@VirtualId [ActivityContainerId], max(c.CustomerId) [CustomerId], 
				max(aic.ActivityTemplateId) [ActivityTemplateId], 
				max(case when f.Name = 'Title' then fv.Value end) [Title],
				max(case when f.Name = 'Assigned To' then fv.Value end) [Assigned To],
				max(case when f.Name = 'Created By' then SUBSTRING(fv.Value,CHARINDEX('.',fv.Value)+1,CHARINDEX('.',fv.Value, CHARINDEX('.',fv.Value)+1)-(CHARINDEX('.',fv.Value)+1)) end) [CreatedBy],
				@RECUREND [RecurDue],
				@RECURSTART [RecurStart], 
				@RECUREND [RecurEnd],
				max(case when f.Name = 'Status' then fv.Value end) [Status],
				max(case when f.Name = 'Assigned Location' then SUBSTRING(fv.Value, 0, CHARINDEX('.', fv.Value)) end) [AssignedLocation],
				max(aic.StatusId) [State],
				max(case when f.Name = 'Recurrence' AND fv.Value = '' then 1 else 0 end) [Recurrence] 
				FROM ActivityItemContainer aic 
				JOIN Container c on c.Id = aic.ContainerId
				JOIN ActivityItemFieldValues afv on afv.ActivityItemContainerId = aic.Id
				JOIN FieldValue fv on fv.Id = afv.FieldValueId
				JOIN Field f on f.Id = fv.FieldId
				WHERE aic.Id = @ActivityItemId
				SET ANSI_WARNINGS ON

				-- Then create the Virtual Recurrence properties
				INSERT INTO VirtualRecurrence(RecurrenceScheduleId, VirtualItemId, StartDateTime, DueDateTime) VALUES (@LATESTRESCHID, @VirtualId, @RECURSTART, @RECUREND)
				DELETE FROM @VIRTUALTABLE

				IF (@FIRSTRECUREVENT is null) SET @FIRSTRECUREVENT = @RECURSTART
				SET @LASTRECUREVENT = @RECUREND
			END
		END
			
		SET @EVENTDATE = DATEADD(DAY, 1, @EVENTDATE)
	END
	ELSE
	BEGIN
		SET @EVENTDATE = DATEADD(DAY, 7, @EVENTDATE)
	END 
END

--#5 update the Parent so the Start Date and End Date are in the correct format to the defined Recurrence

DECLARE @UPDATEDSTARTDATE datetime, @UPDATEDENDDATE datetime
DECLARE @RESCHUPDATE TABLE (id int, activityItemRecurrenceId int, Schedule nvarchar(max))
INSERT INTO @RESCHUPDATE SELECT rs.Id, rs.ActivityItemRecurrenceId, rs.Schedule FROM RecurrenceSchedule rs WHERE rs.ActivityItemRecurrenceId = @AIRID
DECLARE @RECUSCHID int
SET @RECUSCHID = (SELECT TOP 1 rs.Id FROM RecurrenceSchedule rs WHERE rs.ActivityItemRecurrenceId = @AIRID)

IF exists(SELECT * FROM @RESCHUPDATE)
BEGIN
	IF @RECUSCHID is not null
	BEGIN

	SET @UPDATEDSTARTDATE = CONVERT(datetime, (SELECT TOP 1 StartDateTime FROM VirtualRecurrence WHERE RecurrenceScheduleId = @LATESTRESCHID ORDER BY StartDateTime ASC)) 
	SET @UPDATEDENDDATE =  CONVERT(datetime, (SELECT TOP 1 DueDateTime FROM VirtualRecurrence WHERE RecurrenceScheduleId = @LATESTRESCHID ORDER BY DueDateTime DESC))

--	Update StartDateTime and EndDateTime values in ActivityItemRecurrence table
	UPDATE ActivityItemRecurrence
	SET StartDateTime = @UPDATEDSTARTDATE, EndDateTime = @UPDATEDENDDATE
	WHERE Id = @AIRID

--  Update schedule Json (StartDate, EndDate, and EndDate in recurrence) and EndDateTime in RecurrenceSchedule table
	DECLARE @UPDATEJSON nvarchar(MAX), 
	@UPDATEONLYSTARTDATE nvarchar(50), 
	@UPDATEONLYENDDATE nvarchar(50)
	SET @UPDATEONLYSTARTDATE = CONVERT(char(10), @UPDATEDSTARTDATE,126) 
	SET @UPDATEONLYENDDATE = CONVERT(char(10), @UPDATEDENDDATE,126) 
	SET @UPDATEJSON = @JSONSCHEDULE
	SET @UPDATEJSON = JSON_MODIFY(@UPDATEJSON, N'$.startDate', @UPDATEONLYSTARTDATE)
	SET @UPDATEJSON = JSON_MODIFY(@UPDATEJSON, N'$.recurrence.endDate', @UPDATEONLYENDDATE)

	UPDATE RecurrenceSchedule
	SET EndDateTime = @UPDATEDENDDATE, Schedule = @UPDATEJSON
	WHERE Id = @LATESTRESCHID

--  Update FieldId 12 (StartDateTime), FieldId 13 (EndDateTime), and Field 21 (Recurrence) in FieldValue table

	UPDATE FieldValue 
	SET Value = CASE FieldId
				WHEN 12 THEN CONVERT(VARCHAR, @UPDATEDSTARTDATE, 121)
				WHEN 13 THEN CONVERT(VARCHAR, @UPDATEDENDDATE, 121)
				WHEN 21 THEN @UPDATEJSON
				ELSE Value
				END
	FROM FieldValue fv
	JOIN ActivityItemFieldValues aifv on aifv.FieldValueId = fv.Id
	JOIN Field f on f.Id = fv.FieldId
	WHERE aifv.ActivityItemContainerId = @ActivityItemId	

	END
END
