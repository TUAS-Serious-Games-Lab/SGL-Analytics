﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.DTO {
	public record LoginRequestDTO(Guid UserId, string UserSecret);
}
