﻿using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Xbim.InformationSpecifications.NewTests
{
    public class ValueConstraintHelpersTests
    {
        [Fact]
        public void ValueConstraintIsExact()
        {
            ValueConstraint constraint = new ValueConstraint();

            var stringValue = "Gatto";
            constraint = new ValueConstraint(stringValue);
            var test = constraint.IsSingleUndefinedExact(out var someVal);
            test.Should().BeFalse("starting from a string we set the type to string"); 
            someVal.Should().BeNull();

            test = constraint.IsSingleExact(out string gattoVal);
            test.Should().BeTrue();
            gattoVal.Should().Be(stringValue);

            test = constraint.IsSingleExact<int>(out int intGattoVal);
            test.Should().BeFalse();
            intGattoVal.Should().Be(default(int));

            int IntValue = 32;
            constraint = new ValueConstraint(IntValue);
            test = constraint.IsSingleExact(out int tInt);
            test.Should().BeTrue();
            tInt.Should().Be(IntValue);

            test = constraint.IsSingleExact(out string strVal);
            test.Should().BeFalse();
            strVal.Should().Be(default(string));
        }
    }
}